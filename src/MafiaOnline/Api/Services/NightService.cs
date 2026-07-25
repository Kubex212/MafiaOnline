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
    Task SubmitActionAsync(string lobbyCode, Guid actorId, NightAction action);
    Task SkipTurnAsync(string lobbyCode, Guid actorId);
}

public class NightService : INightService
{
    private const int TurnSeconds = 1000;

    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hub;
    private readonly IDayService _dayService;
    private readonly Dictionary<string, CancellationTokenSource> _timers = new();

    public NightService(ILobbyRepository repository, IHubContext<LobbyHub> hub, IDayService dayService)
    {
        _repository = repository;
        _hub = hub;
        _dayService = dayService;
    }

    public async Task BeginNightAsync(string lobbyCode)
    {
        var lobby = await GetLobby(lobbyCode);
        lobby.BeginNightSession();
        await _repository.Update(lobby);
        await AdvanceTurnAsync(lobby);
    }

    public async Task SubmitActionAsync(string lobbyCode, Guid actorId, NightAction action)
    {
        var lobby = await GetLobby(lobbyCode);
        lobby.NightSession!.SubmitAction(actorId, action);
        await _repository.Update(lobby);
        CancelTimer(lobbyCode);
        await AdvanceTurnAsync(lobby);
    }

    public async Task SkipTurnAsync(string lobbyCode, Guid actorId)
    {
        var lobby = await GetLobby(lobbyCode);
        lobby.NightSession!.Skip(actorId);
        await _repository.Update(lobby);
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

        int minTargets = 0;
        int maxTargets = 1;

        if (hasAbility && lobby.Game != null)
        {
            var enginePlayer = lobby.Game.Players.FirstOrDefault(p => p.Id == active.Id);
            if (enginePlayer?.Role is IHasNightAbility na)
            {
                minTargets = na.NightAbility.MinTargets;
                maxTargets = na.NightAbility.MaxTargets;
            }
        }

        int turnSeconds = lobby.Game?.Config.TurnDurationSeconds ?? TurnSeconds;

        await _hub.Clients.Group(lobby.Code).SendAsync("NightTurnStarted", new
        {
            activePlayerId  = active.Id,
            activeSeatIndex = active.SeatIndex,
            hasAbility,
            timeoutSeconds  = turnSeconds,
            minTargets,
            maxTargets
        });

        var cts = new CancellationTokenSource();
        lock (_timers) { _timers[lobby.Code] = cts; }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(turnSeconds), cts.Token);
                var current = await GetLobby(lobby.Code);
                if (current.NightSession?.ActivePlayer?.Id == active.Id)
                {
                    current.NightSession.ForceAdvance();
                    await _repository.Update(current);
                    await AdvanceTurnAsync(current);
                }
            }
            catch (TaskCanceledException) { /* manually advanced */ }
        }, CancellationToken.None);
    }

    private async Task ResolveNightAsync(Lobby lobby)
    {
        var result = lobby.ResolveNight();
        if (lobby.NightSession != null)
            lobby.NightSession.Resolved = true;
        await _repository.Update(lobby);

        var killedNames = result.Killed
            .Select(id => lobby.Players.FirstOrDefault(p => p.Id == id)?.Nickname ?? "?")
            .ToList();

        bool attackBlocked = result.Events.Any(e => e is AttackBlockedEvent);

        await _hub.Clients.Group(lobby.Code).SendAsync("NightResolved", new
        {
            killed = killedNames,
            attackBlocked
        });

        // Check game over
        if (lobby.Game!.Phase == global::MafiaOnline.Engine.GamePhase.Finished)
        {
            await _hub.Clients.Group(lobby.Code).SendAsync("GameOver", new
            {
                winner = lobby.Game.Winner.ToString()
            });
            return;
        }

        // Transition to day after a short pause so clients can show the night summary
        await Task.Delay(7000);
        await _dayService.BeginDayAsync(lobby.Code);
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

    private async Task<Lobby> GetLobby(string code)
        => await _repository.Get(code.ToUpperInvariant())
           ?? throw new InvalidOperationException("Lobby not found.");
}
