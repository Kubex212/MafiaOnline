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
        await AdvanceTurnAsync(lobby);
    }

    public async Task SubmitActionAsync(string lobbyCode, Guid userId, List<Guid> targets, ActionType actionType)
    {
        _logger.LogInformation("SubmitActionAsync lobby={LobbyCode} user={UserId} action={ActionType} targetsCount={TargetsCount}", lobbyCode, userId, actionType, targets?.Count ?? 0);
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
                        gameId = lobby.CurrentGame!.Id,
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
            _logger.LogDebug("Advancing night session for lobby {LobbyCode} actor={ActorId}", lobbyCode, actorId);
            CancelTimer(GetTimerKey(lobby));
            lobby.NightSession.Advance();
            await _repository.Update(lobby);
            await AdvanceTurnAsync(lobby);
        }
    }

    private bool ShouldAdvance(Lobby lobby, Guid userId)
    {
        var isWerewolf = lobby.CurrentGame?.Players.FirstOrDefault(p => p.Id == userId)?.Role.Team == Team.Werewolves;
        var hasAbility = lobby.ActivePlayerHasAbility();

        if (!isWerewolf || !hasAbility)
            return true;

        var submittedAttack = lobby.NightSession?.SubmittedActions?.Any(sa => sa.Actor == userId && sa.ActionType == ActionType.Attack);
        var submittedAbility = lobby.NightSession?.SubmittedActions?.Any(sa => sa.Actor == userId && sa.ActionType == ActionType.Ability);

        return submittedAttack == true && submittedAbility == true;
    }

    public async Task SkipTurnAsync(string lobbyCode, Guid userId)
    {
        _logger.LogInformation("SkipTurnAsync lobby={LobbyCode} user={UserId}", lobbyCode, userId);
        var lobby = await GetLobby(lobbyCode);
        var actorId = GetActorId(lobby, userId);
        lobby.NightSession!.Skip(actorId);
        await _repository.Update(lobby);
        CancelTimer(GetTimerKey(lobby));
        await AdvanceTurnAsync(lobby);
    }

    public async Task AdvanceTurnAsync(Lobby lobby)
    {
        if (lobby.NightSession is null || lobby.NightSession.IsFinished)
        {
            _logger.LogDebug("Night session finished or null for lobby {LobbyCode}", lobby.Code);
            CancelTimer(GetTimerKey(lobby));
            await ResolveNightAsync(lobby);
            return;
        }

        var active = lobby.NightSession.ActivePlayer!;
        var gameId = lobby.CurrentGame?.Id ?? throw new InvalidOperationException("Game not started.");
        _logger.LogInformation("Starting night turn for lobby {LobbyCode} activePlayer={PlayerId} game={GameId}", lobby.Code, active.Id, gameId);
        var hasAbility = lobby.ActivePlayerHasAbility();

        var minTargets = 0;
        var maxTargets = 1;

        if (hasAbility && lobby.CurrentGame != null)
        {
            var enginePlayer = lobby.CurrentGame.Players.FirstOrDefault(p => p.Id == active.Id);
            if (enginePlayer?.Role is IHasNightAbility na)
            {
                minTargets = na.NightAbility.MinTargets;
                maxTargets = na.NightAbility.MaxTargets;
            }
        }

        var turnSeconds = lobby.CurrentGame?.Config.TurnDurationSeconds ?? TurnSeconds;

        await _hub.Clients.Group(lobby.Code).SendAsync("NightTurnStarted", new
        {
            gameId,
            activePlayerId = active.Id,
            activeSeatIndex = active.SeatIndex,
            hasAbility,
            timeoutSeconds = turnSeconds,
            minTargets,
            maxTargets,
            forbiddenTargets = lobby.CurrentGame!.GetForbiddenTargets(active.Id)
        });

        var currentTurnId = lobby.NightSession.CurrentTurnId;
        var timerKey = GetTimerKey(lobby.Code, gameId);
        var cts = new CancellationTokenSource();
        lock (_timers)
        {
            if (_timers.TryGetValue(timerKey, out var previous))
                previous.Cancel();

            _timers[timerKey] = cts;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting night turn timer for lobby {LobbyCode} game={GameId} seconds={Seconds}", lobby.Code, gameId, turnSeconds);
                await Task.Delay(TimeSpan.FromSeconds(turnSeconds), cts.Token);

                lock (_timers)
                {
                    if (!_timers.TryGetValue(timerKey, out var currentTimer) || currentTimer != cts)
                    {
                        _logger.LogDebug("Ignoring stale night timer for lobby {LobbyCode} game={GameId}", lobby.Code, gameId);
                        return;
                    }
                }

                using var scope = _scopeFactory.CreateScope();
                var lobbyService = scope.ServiceProvider.GetRequiredService<ILobbyService>();
                var current = await lobbyService.GetLobby(lobby.Code);
                if (current.CurrentGame?.Id != gameId)
                    return;

                if (current.NightSession?.ActivePlayer?.Id == active.Id && current.NightSession.CurrentTurnId == currentTurnId)
                {
                    _logger.LogInformation("Night turn timer expired for lobby {LobbyCode} game={GameId} activePlayer={PlayerId}, forcing advance", lobby.Code, gameId, active.Id);
                    current.NightSession.ForceAdvance();

                    var repository = scope.ServiceProvider.GetRequiredService<ILobbyRepository>();
                    await repository.Update(current);
                    var nightService = scope.ServiceProvider.GetRequiredService<INightService>();
                    await nightService.AdvanceTurnAsync(current);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Night turn timer cancelled for lobby {LobbyCode} game={GameId}", lobby.Code, gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in night timer task for lobby {LobbyCode} game={GameId}", lobby.Code, gameId);
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

    private async Task ResolveNightAsync(Lobby lobby)
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

        if (lobby.CurrentGame!.Phase == global::MafiaOnline.Engine.GamePhase.Finished)
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

    public int GetTimerSeconds(string lobbyCode)
    {
        lock (_timers)
        {
            var prefix = lobbyCode.ToUpperInvariant() + ":";
            if (_timers.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal)))
                return TurnSeconds;
        }
        return 0;
    }

    private static Guid GetActorId(Lobby lobby, Guid userId)
        => lobby.Players.FirstOrDefault(p => p.UserId == userId)?.Id
           ?? throw new InvalidOperationException("Player not found in lobby.");

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

    private static string GetTimerKey(Lobby lobby)
        => GetTimerKey(lobby.Code, lobby.CurrentGame?.Id ?? Guid.Empty);

    private static string GetTimerKey(string lobbyCode, Guid gameId)
        => $"{lobbyCode.ToUpperInvariant()}:{gameId}";
}
