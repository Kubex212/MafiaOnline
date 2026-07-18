using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using Microsoft.AspNetCore.SignalR;

namespace MafiaOnline.Api.Services;

public interface INightService
{
    Task BeginNightAsync(string lobbyCode);
    Task SubmitActionAsync(string lobbyCode, Guid actorId, Guid targetId);
    Task SkipTurnAsync(string lobbyCode, Guid actorId);
}

public class NightService : INightService
{
    private const int TurnSeconds = 1000;

    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hub;

    // cancellation per lobby so a new turn cancels the previous timer
    private readonly Dictionary<string, CancellationTokenSource> _timers = new();

    public NightService(ILobbyRepository repository, IHubContext<LobbyHub> hub)
    {
        _repository = repository;
        _hub = hub;
    }

    public async Task BeginNightAsync(string lobbyCode)
    {
        var lobby = GetLobby(lobbyCode);
        lobby.BeginNightSession();
        _repository.Update(lobby);
        await AdvanceTurnAsync(lobby);
    }

    public async Task SubmitActionAsync(string lobbyCode, Guid actorId, Guid targetId)
    {
        var lobby = GetLobby(lobbyCode);
        lobby.NightSession!.SubmitAction(actorId, targetId);
        _repository.Update(lobby);
        CancelTimer(lobbyCode);
        await AdvanceTurnAsync(lobby);
    }

    public async Task SkipTurnAsync(string lobbyCode, Guid actorId)
    {
        var lobby = GetLobby(lobbyCode);
        lobby.NightSession!.Skip(actorId);
        _repository.Update(lobby);
        CancelTimer(lobbyCode);
        await AdvanceTurnAsync(lobby);
    }

    // ── internals ──────────────────────────────────────────────────────────

    private async Task AdvanceTurnAsync(Lobby lobby)
    {
        if (lobby.NightSession is null || lobby.NightSession.IsFinished)
        {
            await ResolveNightAsync(lobby);
            return;
        }

        var active = lobby.NightSession.ActivePlayer!;
        bool hasAbility = lobby.ActivePlayerHasAbility();

        // Tell all clients whose turn it is
        await _hub.Clients.Group(lobby.Code).SendAsync("NightTurnStarted", new
        {
            activePlayerId   = active.Id,
            activeSeatIndex  = active.SeatIndex,
            hasAbility,
            timeoutSeconds   = TurnSeconds
        });

        // Start the countdown timer; auto-skip on expiry
        var cts = new CancellationTokenSource();
        lock (_timers) { _timers[lobby.Code] = cts; }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(TurnSeconds), cts.Token);
                // Timer fired — auto-skip this player
                var current = GetLobby(lobby.Code);
                if (current.NightSession?.ActivePlayer?.Id == active.Id)
                {
                    current.NightSession.ForceAdvance();
                    _repository.Update(current);
                    await AdvanceTurnAsync(current);
                }
            }
            catch (TaskCanceledException) { /* manually advanced */ }
        }, CancellationToken.None);
    }

    private async Task ResolveNightAsync(Lobby lobby)
    {
        var result = lobby.ResolveNight();
        _repository.Update(lobby);

        // Build public summary (no role leaks)
        var killedNames = result.Killed
            .Select(id => lobby.Players.FirstOrDefault(p => p.Id == id)?.Nickname ?? "?")
            .ToList();

        bool attackBlocked = result.Events.Any(e => e is AttackBlockedEvent);

        await _hub.Clients.Group(lobby.Code).SendAsync("NightResolved", new
        {
            killed = killedNames,
            attackBlocked
        });
    }

    private void CancelTimer(string code)
    {
        lock (_timers)
        {
            if (_timers.TryGetValue(code, out var cts))
            {
                cts.Cancel();
                _timers.Remove(code);
            }
        }
    }

    private Lobby GetLobby(string code)
        => _repository.Get(code.ToUpperInvariant())
           ?? throw new InvalidOperationException("Lobby not found.");
}
