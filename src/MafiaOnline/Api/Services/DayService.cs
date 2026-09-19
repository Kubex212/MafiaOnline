using Api.Dtos;
using Api.Services;
using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace MafiaOnline.Api.Services;

public interface IDayService
{
    Task BeginDayAsync(string lobbyCode);
    Task CastVoteAsync(string lobbyCode, Guid userId, Guid? candidateId);
    Task ResolveDayAsync(string lobbyCode);
}

public class DayService : IDayService
{
    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hub;
    private readonly Lazy<INightService> _night_service;
    private readonly Dictionary<string, CancellationTokenSource> _timers = new();
    private readonly IGameHistoryService _gameHistory_service;
    private readonly ILogger<DayService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DayService(ILobbyRepository repository, IHubContext<LobbyHub> hub, Lazy<INightService> nightService, IGameHistoryService gameHistoryService, ILogger<DayService> logger, IServiceScopeFactory scopeFactory)
    {
        _repository = repository;
        _hub = hub;
        _night_service = nightService;
        _gameHistory_service = gameHistoryService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task BeginDayAsync(string lobbyCode)
    {
        _logger.LogInformation("BeginDayAsync called for lobby {LobbyCode}", lobbyCode);
        var lobby = await GetLobby(lobbyCode);
        lobby.BeginDaySession();
        await _repository.Update(lobby);

        var game = lobby.CurrentGame!;
        var duration = game.Config.Day.DurationSeconds;
        var showTargets = game.Config.Day.ShowVoteTargets;

        var last = game.LastNightResult;
        var enshrouded = new List<Guid>();
        if (last != null)
        {
            foreach (var ev in last.Events)
            {
                if (ev is DraculaEnshroudedEvent d)
                    enshrouded.Add(d.TargetId);
            }
        }

        var asleep = new List<Guid>();
        if (last != null)
        {
            foreach (var ev in last.Events)
            {
                if (ev is CharmerSleepEvent c)
                    asleep.Add(c.TargetId);
            }
        }

        var alivePlayers = lobby.Players
            .Where(p => !p.IsSpectator && game.Players.First(ep => ep.Id == p.Id).IsAlive)
            .Select(p => new { id = p.Id, nickname = p.Nickname, seatIndex = p.SeatIndex })
            .ToList();

        await _hub.Clients.Group(lobby.Code).SendAsync("DayStarted", new
        {
            gameId = game.Id,
            durationSeconds = duration,
            showVoteTargets = showTargets.ToString(),
            alivePlayers,
            enshrouded,
            asleep
        });

        _logger.LogInformation("DayStarted broadcast for lobby {LobbyCode} duration={Duration} alivePlayers={Count}", lobbyCode, duration, alivePlayers.Count);

        var currentId = lobby.DaySession!.CurrentTurnId;
        var timerKey = GetTimerKey(lobby.Code, game.Id);
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
                _logger.LogDebug("Starting day timer for lobby {LobbyCode} game={GameId} seconds={Seconds}", lobbyCode, game.Id, duration);
                await Task.Delay(TimeSpan.FromSeconds(duration), cts.Token);

                var lobbyService = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ILobbyService>();
                var current = await lobbyService.GetLobby(lobby.Code);

                if (current.CurrentGame?.Id != game.Id || current.DaySession is null || current.DaySession.CurrentTurnId != currentId)
                    return;

                var dayService = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDayService>();
                await dayService.ResolveDayAsync(lobbyCode);
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Day timer cancelled for lobby {LobbyCode} game={GameId}", lobbyCode, game.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in day timer task for lobby {LobbyCode} game={GameId}", lobbyCode, game.Id);
            }
        }, CancellationToken.None);
    }

    public async Task CastVoteAsync(string lobbyCode, Guid userId, Guid? candidateId)
    {
        _logger.LogInformation("CastVoteAsync lobby={LobbyCode} user={UserId} candidate={CandidateId}", lobbyCode, userId, candidateId);
        var lobby = await GetLobby(lobbyCode);

        if (lobby.DaySession is null)
            throw new InvalidOperationException("No active day session.");

        var voterId = lobby.Players.FirstOrDefault(p => p.UserId == userId)?.Id
            ?? throw new InvalidOperationException("Player not found in lobby.");

        var votes = lobby.CastVote(voterId, candidateId);
        await _repository.Update(lobby);

        var showTargets = lobby.CurrentGame!.Config.Day.ShowVoteTargets;

        await _hub.Clients.Group(lobby.Code).SendAsync("VoteCast", new
        {
            gameId = lobby.CurrentGame.Id,
            voterId,
            candidateId = showTargets != Engine.Config.ShowDayVotes.Never ? (Guid?)candidateId : null,
            totalVotesCast = votes.Count
        });

        _logger.LogDebug("VoteCast broadcast lobby={LobbyCode} voter={VoterId} totalVotesCast={Total}", lobbyCode, voterId, votes.Count);

        // If all players voted or the result can be determined early, resolve the day
        if (ShouldResolveDay(lobby))
        {
            _logger.LogInformation("Resolving day early for lobby {LobbyCode}", lobbyCode);
            CancelTimer(GetTimerKey(lobby.Code, lobby.CurrentGame!.Id));
            await ResolveDayAsync(lobbyCode);
        }
    }

    public async Task ResolveDayAsync(string lobbyCode)
    {
        _logger.LogInformation("ResolveDayAsync called for lobby {LobbyCode}", lobbyCode);
        var lobby = await GetLobby(lobbyCode);

        if (lobby.DaySession is null) return;

        var game = lobby.CurrentGame ?? throw new InvalidOperationException("Game not started.");
        var gameId = game.Id;
        var result = lobby.ResolveDay();
        await _repository.Update(lobby);

        var enshrouded = new HashSet<Guid>();
        var last = game.LastNightResult;
        if (last != null)
        {
            foreach (var ev in last.Events)
            {
                if (ev is DraculaEnshroudedEvent d)
                    enshrouded.Add(d.TargetId);
            }
        }

        var filteredBanished = result.Banished.Where(id => !enshrouded.Contains(id)).ToList();

        var banishedNames = filteredBanished
            .Select(id => lobby.Players.FirstOrDefault(p => p.Id == id)?.Nickname ?? "?")
            .ToList();

        var tally = result.VoteTally
            .Select(kvp => new { playerId = kvp.Key, votes = kvp.Value })
            .ToList();

        await _hub.Clients.Group(lobby.Code).SendAsync("DayResolved", new
        {
            gameId,
            banished = banishedNames,
            thresholdMet = result.ThresholdMet,
            tally
        });

        _logger.LogInformation("DayResolved broadcast for lobby {LobbyCode} banishedCount={Count} thresholdMet={Threshold}", lobbyCode, banishedNames.Count, result.ThresholdMet);

        await Task.Delay(5000);

        var refreshed = await _repository.Get(lobby.Code);
        if (refreshed?.CurrentGame?.Id != gameId)
            return;

        if (refreshed.CurrentGame!.Phase == global::MafiaOnline.Engine.GamePhase.Finished)
        {
            _logger.LogInformation("Game finished for lobby {LobbyCode}, recording history", lobbyCode);
            await _gameHistory_service.RecordGameAsync(refreshed);

            await _hub.Clients.Group(lobby.Code).SendAsync("GameOver", new GameOverPayload
            {
                Winner = refreshed.CurrentGame.Winner.ToString(),
                DrunkId = refreshed.CurrentGame.DrunkPlayer?.Id,
                DrunkName = refreshed.CurrentGame.DrunkPlayer != null ? refreshed.Players.FirstOrDefault(p => p.Id == refreshed.CurrentGame.DrunkPlayer.Id)?.Nickname : null,
                RematchCode = refreshed.Code
            });
            return;
        }

        await _night_service.Value.BeginNightAsync(lobbyCode);
    }

    private void CancelTimer(string key)
    {
        lock (_timers)
        {
            if (_timers.TryGetValue(key, out var cts))
            {
                _logger.LogDebug("Cancelling day timer {TimerKey}", key);
                cts.Cancel();
                _timers.Remove(key);
            }
        }
    }

    private async Task<Lobby> GetLobby(string code)
        => await _repository.Get(code.ToUpperInvariant())
           ?? throw new InvalidOperationException($"Lobby {code} not found.");

    private static string GetTimerKey(string lobbyCode, Guid gameId)
        => $"{lobbyCode.ToUpperInvariant()}:{gameId}";

    // Determine whether the day voting can be resolved early based on current votes and config.
    private bool ShouldResolveDay(Lobby lobby)
    {
        var session = lobby.DaySession;
        var game = lobby.CurrentGame;
        if (session is null || game is null) return false;

        var config = game.Config.Day;

        // Count alive players according to the game state
        var alivePlayers = lobby.Players.Where(p => !p.IsSpectator && game.Players.First(ep => ep.Id == p.Id).IsAlive).Select(p => p.Id).ToList();
        var aliveCount = alivePlayers.Count;

        // Votes map (includes skips as null)
        var votesMap = session.Votes;

        // Build tally for non-skip votes
        var tally = new Dictionary<Guid, int>();
        foreach (var kvp in votesMap)
        {
            if (!kvp.Value.HasValue) continue;
            var candidate = kvp.Value.Value;
            tally.TryGetValue(candidate, out var cur);
            tally[candidate] = cur + 1;
        }

        int maxVotes = tally.Any() ? tally.Values.Max() : 0;
        int requiredVotes = (int)Math.Ceiling(aliveCount * config.VotesRequiredFraction);

        // Remaining players who haven't cast a vote yet
        var remaining = Math.Max(0, aliveCount - votesMap.Count);

        // If nobody can possibly reach the threshold even if all remaining votes go to a single candidate
        var maxPossibleAny = (tally.Any() ? tally.Values.Max() : 0) + remaining;
        if (maxPossibleAny < requiredVotes)
            return true;

        // If no votes yet, cannot resolve early
        if (!tally.Any())
            return false;

        // If threshold already met by some candidate, ensure no other candidate can also meet it
        if (maxVotes >= requiredVotes)
        {
            // Candidates currently at maxVotes
            var currentLeaders = tally.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToList();

            // If more than one leader already, the draw is already decided by config (tie among top)
            if (currentLeaders.Count > 1)
                return true;

            // Compute max possible votes for every other alive candidate (including those with 0 votes)
            foreach (var pid in alivePlayers)
            {
                if (currentLeaders.Contains(pid)) continue; // skip current leader
                tally.TryGetValue(pid, out var curVotes);
                var maxPossibleForPid = curVotes + remaining;
                // If any other candidate could still reach the required threshold, we cannot resolve yet
                if (maxPossibleForPid >= requiredVotes)
                    return false;
            }

            // No other candidate can reach threshold -> safe to resolve
            return true;
        }

        if (session.AllVoted)
            return true;

        return false;
    }
}
