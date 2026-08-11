using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Engine;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using Microsoft.AspNetCore.SignalR;

namespace MafiaOnline.Api.Services;

public interface INightService
{
    Task BeginNightAsync(string lobbyCode);
    Task SubmitActionAsync(string lobbyCode, Guid userId, List<Guid> targets, ActionType actionType);
    Task SkipTurnAsync(string lobbyCode, Guid userId);
    Task<ClueResponse?> GetClueAsync(string lobbyCode, Guid userId);
    Task AdvanceTurnAsync(Lobby lobby);
}

public class NightService : INightService
{
    private const int TurnSeconds = 1000;

    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hub;
    private readonly IDayService _day_service;
    private readonly Dictionary<string, CancellationTokenSource> _timers = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public NightService(ILobbyRepository repository, IHubContext<LobbyHub> hub, IDayService dayService, IServiceScopeFactory serviceScopeFactory)
    {
        _repository = repository;
        _hub = hub;
        _day_service = dayService;
        _scopeFactory = serviceScopeFactory;
    }

    public async Task BeginNightAsync(string lobbyCode)
    {
        var lobby = await GetLobby(lobbyCode);
        lobby.BeginNightSession();
        await _repository.Update(lobby);
        await AdvanceTurnAsync(lobby);
    }

    public async Task SubmitActionAsync(string lobbyCode, Guid userId, List<Guid> targets, ActionType actionType)
    {
        var lobby = await GetLobby(lobbyCode);
        var actorId = GetActorId(lobby, userId);
        var action = new NightAction(actorId, targets, actionType);

        lobby.NightSession!.SubmitAction(action);
        await _repository.Update(lobby);

        if (actionType == ActionType.Attack)
        {
            var cfg = lobby.Config?.Werewolf;
            if (cfg?.ShowKillVotesToTeam ?? true)
            {
                var attackTarget = action.Targets.FirstOrDefault();

                if (action.Targets.Any())
                {
                    var targetName = lobby.Players.FirstOrDefault(p => p.Id == attackTarget)?.Nickname ?? "?";
                    await _hub.Clients.Group(lobby.Code).SendAsync("WerewolfAttackCast", new
                    {
                        actorId,
                        actorName = lobby.Players.FirstOrDefault(p => p.Id == actorId)?.Nickname ?? "?",
                        targetId = attackTarget,
                        targetName,
                    });
                }
            }
        }

        if (ShouldAdvance(lobby, actorId))
        {
            lobby.NightSession.Advance();
            await _repository.Update(lobby);

            await AdvanceTurnAsync(lobby);
            CancelTimer(lobbyCode);
        }
        return;
    }

    private bool ShouldAdvance(Lobby lobby, Guid userId)
    {
        // determine if this is the second action of a werewolf (attack + ability) or if the player has no ability and thus should advance
        var isWerewolf = lobby.Game?.Players.FirstOrDefault(p => p.Id == userId)?.Role.Team == Team.Werewolves;
        var hasAbility = lobby.ActivePlayerHasAbility();

        if(!isWerewolf || !hasAbility)
            return true;

        // check if the player has already submitted an attack
        var submittedAttack = lobby.NightSession?.SubmittedActions?.Any(sa => sa.Actor == userId && sa.ActionType == ActionType.Attack);
        var submittedAbility = lobby.NightSession?.SubmittedActions?.Any(sa => sa.Actor == userId && sa.ActionType == ActionType.Ability);

        return submittedAttack == true && submittedAbility == true;
    }

    public async Task SkipTurnAsync(string lobbyCode, Guid userId)
    {
        var lobby = await GetLobby(lobbyCode);
        var actorId = GetActorId(lobby, userId);
        lobby.NightSession!.Skip(actorId);
        await _repository.Update(lobby);
        CancelTimer(lobbyCode);
        await AdvanceTurnAsync(lobby);
    }


    public async Task AdvanceTurnAsync(Lobby lobby)
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
            maxTargets,
            forbiddenTargets = lobby.Game!.GetForbiddenTargets(active.Id)
        });

        var cts = new CancellationTokenSource();
        lock (_timers) { _timers[lobby.Code] = cts; }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(turnSeconds), cts.Token);
                using var scope = _scopeFactory.CreateScope();
                var lobbyService = scope.ServiceProvider.GetRequiredService<ILobbyService>();
                var current = await lobbyService.GetLobby(lobby.Code);
                if (current.NightSession?.ActivePlayer?.Id == active.Id)
                {
                    current.NightSession.ForceAdvance();

                    var repository = scope.ServiceProvider.GetRequiredService<ILobbyRepository>();
                    await repository.Update(current);
                    var nightService = scope.ServiceProvider.GetRequiredService<INightService>();
                    await nightService.AdvanceTurnAsync(current);
                }
            }
            catch (TaskCanceledException)
            {
                // manually advanced
            }
        }, CancellationToken.None);
    }


    // ── internals ──────────────────────────────────────────────────────────

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
        await _day_service.BeginDayAsync(lobby.Code);
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

    public int GetTimerSeconds(string lobbyCode)
    {
        lock (_timers)
        {
            if (_timers.TryGetValue(lobbyCode, out var cts))
            {
                // Return the remaining time in seconds
                return (int)(cts.Token.WaitHandle.WaitOne(0) ? 0 : TurnSeconds);
            }
        }
        return 0;
    }

    private static Guid GetActorId(Lobby lobby, Guid userId)
        => lobby.Players.FirstOrDefault(p => p.UserId == userId)?.Id
           ?? throw new InvalidOperationException("Player not found in lobby.");

    public async Task<ClueResponse?> GetClueAsync(string code, Guid userId)
    {
        var lobby = await _repository.Get(code);

        if (lobby?.Game is null)
            return null;

        var player = lobby.Players.FirstOrDefault(p => p.UserId == userId);
        if (player is null)
            throw new ArgumentException("player not found in lobby", nameof(userId));

        var result = lobby.Game.LastNightResult;
        if (result is null)
            return null;

        var clueEvent = result.Events
            .OfType<ClueEvent>()
            .SingleOrDefault(e => e.ClueOwnerId == player.Id);

        if (clueEvent is null)
            return null;

        return new ClueResponse
        {
            Type = clueEvent.ClueType.ToString(),
            Clue = clueEvent.DisplayedClue,
            Data = clueEvent.GetAdditionalInfoDictionary(lobby.Game.Players)
        };
    }
}
