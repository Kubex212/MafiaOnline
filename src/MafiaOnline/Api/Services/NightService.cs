using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Engine;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MafiaOnline.Api.Services;

public interface INightService
{
    Task BeginNightAsync(string lobbyCode);
    Task SubmitActionAsync(string lobbyCode, Guid userId, List<Guid> targets, ActionType actionType);
    Task SkipTurnAsync(string lobbyCode, Guid userId);
    Task<ClueResponse?> GetClueAsync(string lobbyCode, Guid userId);
    Task ResolveNightAsync(Lobby lobby);
}

public class NightService : INightService
{
    private const int NightDurationSeconds = 1000;

    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hub;
    private readonly IDayService _day_service;
    private readonly Dictionary<string, CancellationTokenSource> _timers = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NightService> _logger;

    public NightService(ILobbyRepository repository, IHubContext<LobbyHub> hub, IDayService dayService, IServiceScopeFactory serviceScopeFactory, ILogger<NightService> logger)
    {
        _repository = repository;
        _hub = hub;
        _day_service = dayService;
        _scopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task BeginNightAsync(string lobbyCode)
    {
        _logger.LogInformation("BeginNightAsync called for lobby {LobbyCode}", lobbyCode);
        var lobby = await GetLobby(lobbyCode);
        lobby.BeginNightSession();
        await _repository.Update(lobby);

        var game = lobby.CurrentGame ?? throw new InvalidOperationException("Game not started.");
        var gameId = game.Id;
        var alivePlayers = lobby.Players.Where(p => game.Players.First(ep => ep.Id == p.Id).IsAlive).ToList();

        // Get info for each player: do they have an ability?
        var playerInfos = new List<dynamic>();
        foreach (var player in alivePlayers)
        {
            var hasAbility = lobby.PlayerHasAbility(player.Id);
            var minTargets = 0;
            var maxTargets = 1;

            if (hasAbility && lobby.CurrentGame != null)
            {
                var enginePlayer = lobby.CurrentGame.Players.FirstOrDefault(p => p.Id == player.Id);
                if (enginePlayer?.Role is IHasNightAbility na)
                {
                    minTargets = na.NightAbility.MinTargets;
                    maxTargets = na.NightAbility.MaxTargets;
                }
            }

            var forbiddenIds = lobby.CurrentGame!.GetForbiddenTargets(player.Id);
            _logger.LogDebug("Night targets for {Nickname} ({PlayerId}): hasAbility={HasAbility}, forbidden={ForbiddenCount}, forbiddenList={Forbidden}",
                player.Nickname, player.Id, hasAbility, forbiddenIds.Count, string.Join(",", forbiddenIds.Take(3)));

            playerInfos.Add(new
            {
                playerId = player.Id,
                hasAbility,
                minTargets,
                maxTargets,
                forbiddenTargets = forbiddenIds
            });
        }

        var nightDurationSeconds = lobby.CurrentGame?.Config.TurnDurationSeconds ?? NightDurationSeconds;

        await _hub.Clients.Group(lobby.Code).SendAsync("NightStarted", new
        {
            gameId,
            durationSeconds = nightDurationSeconds,
            playerInfos,
            allAlivePlayers = alivePlayers.Select(p => new { id = p.Id, nickname = p.Nickname, seatIndex = p.SeatIndex }).ToList()
        });

        _logger.LogInformation("NightStarted broadcast for lobby {LobbyCode} duration={Duration} alivePlayers={Count}", lobbyCode, nightDurationSeconds, alivePlayers.Count);

        var timerKey = GetTimerKey(lobby.Code, gameId);
        var cts = new CancellationTokenSource();
        lock (_timers)
        {
            if (_timers.TryGetValue(timerKey, out var previous))
                previous.Cancel();
            _timers[timerKey] = cts;
        }

        var sessionStartId = lobby.NightSession!.SessionStartId;
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting night timer for lobby {LobbyCode} game={GameId} seconds={Seconds}", lobbyCode, gameId, nightDurationSeconds);
                await Task.Delay(TimeSpan.FromSeconds(nightDurationSeconds), cts.Token);

                using var scope = _scopeFactory.CreateScope();
                var lobbyService = scope.ServiceProvider.GetRequiredService<ILobbyService>();
                var current = await lobbyService.GetLobby(lobby.Code);

                if (current.CurrentGame?.Id != gameId || current.NightSession is null || current.NightSession.SessionStartId != sessionStartId)
                {
                    _logger.LogDebug("Night timer expired but session has changed for lobby {LobbyCode}", lobbyCode);
                    return;
                }

                _logger.LogInformation("Night timer expired for lobby {LobbyCode} game={GameId}, resolving night", lobbyCode, gameId);
                var lobbyRepository = scope.ServiceProvider.GetRequiredService<ILobbyRepository>();
                var lobbyToResolve = await lobbyRepository.Get(lobby.Code);
                if (lobbyToResolve?.CurrentGame?.Id == gameId && lobbyToResolve.NightSession != null)
                {
                    var nightService = scope.ServiceProvider.GetRequiredService<INightService>();
                    await nightService.ResolveNightAsync(lobbyToResolve);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Night timer cancelled for lobby {LobbyCode} game={GameId}", lobbyCode, gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in night timer task for lobby {LobbyCode} game={GameId}", lobbyCode, gameId);
            }
            finally
            {
                lock (_timers)
                {
                    if (_timers.TryGetValue(timerKey, out var current) && current == cts)
                        _timers.Remove(timerKey);
                }
                cts.Dispose();
            }
        }, CancellationToken.None);
    }

    public async Task SubmitActionAsync(string lobbyCode, Guid userId, List<Guid> targets, ActionType actionType)
    {
        _logger.LogInformation("SubmitActionAsync lobby={LobbyCode} user={UserId} action={ActionType} targetsCount={TargetsCount}", lobbyCode, userId, actionType, targets?.Count ?? 0);
        var lobby = await GetLobby(lobbyCode);
        var actorId = GetActorId(lobby, userId);
        var action = new NightAction(actorId, targets, actionType);

        lobby.NightSession!.SubmitAction(action);

        var gameId = lobby.CurrentGame?.Id ?? throw new InvalidOperationException("Game not started.");

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
                        gameId,
                        actorId,
                        actorName = lobby.Players.FirstOrDefault(p => p.Id == actorId)?.Nickname ?? "?",
                        targetId = attackTarget,
                        targetName,
                    });
                }
            }
        }

        // Check if this player is now done with all their required actions
        bool playerJustFinished = TryMarkPlayerDone(lobby, actorId);
        await _repository.Update(lobby);

        // Only broadcast ActionSubmitted if player is truly done
        if (playerJustFinished)
        {
            await _hub.Clients.Group(lobby.Code).SendAsync("TurnFinished", new
            {
                gameId,
                actorId,
                actorName = lobby.Players.FirstOrDefault(p => p.Id == actorId)?.Nickname ?? "?"
            });

            _logger.LogDebug("Player completed all actions for lobby {LobbyCode} actor={ActorId} playerStillPending={Count}", 
                lobbyCode, actorId, lobby.NightSession.PlayersPendingAction.Count);
        }
        else
        {
            _logger.LogDebug("Player submitted action for lobby {LobbyCode} actor={ActorId} action={ActionType} (not done yet)", 
                lobbyCode, actorId, actionType);
        }

        if (IsNightComplete(lobby))
        {
            _logger.LogInformation("All players submitted actions for lobby {LobbyCode}, resolving night immediately", lobbyCode);
            CancelTimer(GetTimerKey(lobby.Code, gameId));
            await ResolveNightAsync(lobby);
        }
    }

    public async Task SkipTurnAsync(string lobbyCode, Guid userId)
    {
        _logger.LogInformation("SkipTurnAsync lobby={LobbyCode} user={UserId}", lobbyCode, userId);
        var lobby = await GetLobby(lobbyCode);
        var actorId = GetActorId(lobby, userId);
        lobby.NightSession!.Skip(actorId);
        await _repository.Update(lobby);

        var gameId = lobby.CurrentGame?.Id ?? throw new InvalidOperationException("Game not started.");

        // Broadcast that this player has submitted/completed their action (skip)
        await _hub.Clients.Group(lobby.Code).SendAsync("TurnFinished", new
        {
            gameId,
            actorId,
            actorName = lobby.Players.FirstOrDefault(p => p.Id == actorId)?.Nickname ?? "?"
        });

        _logger.LogDebug("Player skipped for lobby {LobbyCode} actor={ActorId} playerStillPending={Count}", 
            lobbyCode, actorId, lobby.NightSession.PlayersPendingAction.Count);

        if (IsNightComplete(lobby))
        {
            _logger.LogInformation("All players submitted actions for lobby {LobbyCode}, resolving night immediately", lobbyCode);
            CancelTimer(GetTimerKey(lobby.Code, gameId));
            await ResolveNightAsync(lobby);
        }
    }

    /// <summary>
    /// Check if a player has completed all their required actions and mark them done if so.
    /// Returns true only if player was marked as done (i.e., completed all requirements).
    /// Werewolves with abilities must submit both attack and ability.
    /// Others submit one action or skip.
    /// </summary>
    private bool TryMarkPlayerDone(Lobby lobby, Guid playerId)
    {
        if (lobby.NightSession is null || lobby.CurrentGame is null)
            return false;

        var game = lobby.CurrentGame;
        var enginePlayer = game.Players.FirstOrDefault(p => p.Id == playerId);
        if (enginePlayer is null)
            return false;

        var submitted = lobby.NightSession.SubmittedActions;
        var isWerewolf = enginePlayer.Role.Team == Team.Werewolves;
        var hasAbility = lobby.PlayerHasAbility(playerId);

        if (isWerewolf)
        {
            // Werewolves must submit attack
            var hasAttack = submitted.Any(a => a.Actor == playerId && a.ActionType == ActionType.Attack);
            if (!hasAttack)
                return false; // Not done yet

            // Werewolves must submit ability if they have one
            if (hasAbility)
            {
                var hasAbilityAction = submitted.Any(a => a.Actor == playerId && a.ActionType == ActionType.Ability);
                if (!hasAbilityAction)
                    return false; // Not done yet - still waiting for ability
            }

            // Werewolf has submitted all required actions
            lobby.NightSession.MarkPlayerDone(playerId);
            return true;
        }
        else
        {
            // Non-werewolves are done as soon as they submit one action or skip
            lobby.NightSession.MarkPlayerDone(playerId);
            return true;
        }
    }

    /// <summary>
    /// Check if night is complete. All players must be done with their required actions.
    /// </summary>
    private bool IsNightComplete(Lobby lobby)
    {
        if (lobby.NightSession is null)
            return false;
        
        return lobby.NightSession.PlayersPendingAction.Count == 0;
    }

    public async Task ResolveNightAsync(Lobby lobby)
    {
        _logger.LogInformation("Resolving night for lobby {LobbyCode}", lobby.Code);
        var gameId = lobby.CurrentGame?.Id ?? throw new InvalidOperationException("Game not started.");
        var result = lobby.ResolveNight();
        await _repository.Update(lobby);

        var killedNames = result.Killed
            .Select(id => lobby.Players.FirstOrDefault(p => p.Id == id)?.Nickname ?? "?")
            .ToList();

        var attackBlocked = result.Events.Any(e => e is AttackBlockedEvent);

        await _hub.Clients.Group(lobby.Code).SendAsync("NightResolved", new
        {
            gameId,
            killed = killedNames,
            attackBlocked
        });

        if (lobby.CurrentGame!.Phase == GamePhase.Finished)
        {
            _logger.LogInformation("Game over for lobby {LobbyCode} winner={Winner}", lobby.Code, lobby.CurrentGame.Winner);

            await _hub.Clients.Group(lobby.Code).SendAsync("GameOver", new
            {
                gameId,
                winner = lobby.CurrentGame.Winner.ToString()
            });
            return;
        }

        await Task.Delay(7000);

        var refreshed = await _repository.Get(lobby.Code);
        if (refreshed?.CurrentGame?.Id != gameId)
            return;

        await _day_service.BeginDayAsync(lobby.Code);
    }

    private void CancelTimer(string key)
    {
        lock (_timers)
        {
            if (_timers.TryGetValue(key, out var cts))
            {
                _logger.LogDebug("Cancelling timer {TimerKey}", key);
                cts.Cancel();
                _timers.Remove(key);
            }
        }
    }

    private async Task<Lobby> GetLobby(string code)
        => await _repository.Get(code.ToUpperInvariant())
           ?? throw new InvalidOperationException("Lobby not found.");

    private static Guid GetActorId(Lobby lobby, Guid userId)
        => lobby.Players.FirstOrDefault(p => p.UserId == userId)?.Id
           ?? throw new InvalidOperationException("Player not found in lobby.");

    private static string GetTimerKey(string lobbyCode, Guid gameId)
        => $"{lobbyCode.ToUpperInvariant()}:{gameId}";

    public async Task<ClueResponse?> GetClueAsync(string code, Guid userId)
    {
        var lobby = await _repository.Get(code);

        if (lobby?.CurrentGame is null)
            return null;

        var player = lobby.Players.FirstOrDefault(p => p.UserId == userId);
        if (player is null)
            throw new ArgumentException("player not found in lobby", nameof(userId));

        var result = lobby.CurrentGame.LastNightResult;
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
            Data = clueEvent.GetAdditionalInfoDictionary(lobby.CurrentGame.Players)
        };
    }
}
