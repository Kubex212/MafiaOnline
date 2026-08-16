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
}

public class DayService : IDayService
{
    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hub;
    private readonly Lazy<INightService> _night_service;
    private readonly Dictionary<string, CancellationTokenSource> _timers = new();
    private readonly IGameHistoryService _gameHistory_service;
    private readonly ILogger<DayService> _logger;

    public DayService(ILobbyRepository repository, IHubContext<LobbyHub> hub, Lazy<INightService> nightService, IGameHistoryService gameHistoryService, ILogger<DayService> logger)
    {
        _repository = repository;
        _hub = hub;
        _night_service = nightService;
        _gameHistory_service = gameHistoryService;
        _logger = logger;
    }

    public async Task BeginDayAsync(string lobbyCode)
    {
        _logger.LogInformation("BeginDayAsync called for lobby {LobbyCode}", lobbyCode);
        var lobby = await GetLobby(lobbyCode);
        lobby.BeginDaySession();
        await _repository.Update(lobby);

        var duration = lobby.Game!.Config.Day.DurationSeconds;
        var showTargets = lobby.Game.Config.Day.ShowVoteTargets;

        // determine enshrouded and sleeping players from last night, if any
        var last = lobby.Game?.LastNightResult;
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

        // Tell all clients the day has started
        var alivePlayers = lobby.Players
            .Where(p => lobby.Game.Players.First(ep => ep.Id == p.Id).IsAlive)
            .Select(p => new { id = p.Id, nickname = p.Nickname, seatIndex = p.SeatIndex })
            .ToList();

        await _hub.Clients.Group(lobby.Code).SendAsync("DayStarted", new
        {
            durationSeconds = duration,
            showVoteTargets = showTargets.ToString(),
            alivePlayers,
            enshrouded,
            asleep
        });

        _logger.LogInformation("DayStarted broadcast for lobby {LobbyCode} duration={Duration} alivePlayers={Count}", lobbyCode, duration, alivePlayers.Count);

        // Start day countdown timer
        var cts = new CancellationTokenSource();
        lock (_timers) { _timers[lobby.Code] = cts; }

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting day timer for lobby {LobbyCode} seconds={Seconds}", lobbyCode, duration);
                await Task.Delay(TimeSpan.FromSeconds(duration), cts.Token);
                _logger.LogInformation("Day timer expired for lobby {LobbyCode}", lobbyCode);
                await ResolveDayAsync(lobbyCode);
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Day timer cancelled for lobby {LobbyCode}", lobbyCode);
                /* all players voted early */
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in day timer task for lobby {LobbyCode}", lobbyCode);
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

        var alreadyVoted = lobby.DaySession.HasVoted(voterId);
        var votes = lobby.CastVote(voterId, candidateId);
        await _repository.Update(lobby);

        var showTargets = lobby.Game!.Config.Day.ShowVoteTargets;

        await _hub.Clients.Group(lobby.Code).SendAsync("VoteCast", new
        {
            voterId,
            candidateId = showTargets != Engine.Config.ShowDayVotes.Never ? (Guid?)candidateId : null,
            totalVotesCast = votes.Count
        });

        _logger.LogDebug("VoteCast broadcast lobby={LobbyCode} voter={VoterId} totalVotesCast={Total}", lobbyCode, voterId, votes.Count);

        if (lobby.DaySession.AllVoted)
        {
            _logger.LogInformation("All players voted for lobby {LobbyCode}, resolving day", lobbyCode);
            CancelTimer(lobbyCode);
            await ResolveDayAsync(lobbyCode);
        }
    }

    private async Task ResolveDayAsync(string lobbyCode)
    {
        _logger.LogInformation("ResolveDayAsync called for lobby {LobbyCode}", lobbyCode);
        var lobby = await GetLobby(lobbyCode);

        if (lobby.DaySession is null) return; // already resolved (timer race)

        var result = lobby.ResolveDay();
        if (lobby.DaySession != null)
            lobby.DaySession.Resolved = true;
        await _repository.Update(lobby);

        // Determine enshrouded players from last night, if any (so DayResolved can also reflect)
        var enshrouded = new HashSet<Guid>();
        var last = lobby.Game?.LastNightResult;
        if (last != null)
        {
            foreach (var ev in last.Events)
            {
                if (ev is DraculaEnshroudedEvent d)
                    enshrouded.Add(d.TargetId);
            }
        }

        // Filter out enshrouded players from banishment
        var filteredBanished = result.Banished.Where(id => !enshrouded.Contains(id)).ToList();

        var banishedNames = filteredBanished
            .Select(id => lobby.Players.FirstOrDefault(p => p.Id == id)?.Nickname ?? "?")
            .ToList();

        // Build a public tally (player IDs only — names resolved on client from seat list)
        var tally = result.VoteTally
            .Select(kvp => new { playerId = kvp.Key, votes = kvp.Value })
            .ToList();

        await _hub.Clients.Group(lobby.Code).SendAsync("DayResolved", new
        {
            banished = banishedNames,
            thresholdMet = result.ThresholdMet,
            tally
        });

        _logger.LogInformation("DayResolved broadcast for lobby {LobbyCode} banishedCount={Count} thresholdMet={Threshold}", lobbyCode, banishedNames.Count, result.ThresholdMet);

        // wait a few seconds before transitioning to night/gameover so players can see the results
        await Task.Delay(5000);

        // Check game over
        if (lobby.Game!.Phase == global::MafiaOnline.Engine.GamePhase.Finished)
        {
            _logger.LogInformation("Game finished for lobby {LobbyCode}, recording history", lobbyCode);
            await _gameHistory_service.RecordGameAsync(lobby);

            await _hub.Clients.Group(lobby.Code).SendAsync("GameOver", new GameOverPayload
            {
                Winner = lobby.Game.Winner.ToString(),
                DrunkId = lobby.Game.DrunkPlayer?.Id,
                DrunkName = lobby.Game.DrunkPlayer != null ? lobby.Players.FirstOrDefault(p => p.Id == lobby.Game.DrunkPlayer.Id)?.Nickname : null
            });
            return;
        }

        // transition back to night
        await _night_service.Value.BeginNightAsync(lobbyCode);
    }

    private void CancelTimer(string code)
    {
        lock (_timers)
        {
            if (_timers.TryGetValue(code, out var cts))
            {
                _logger.LogDebug("Cancelling day timer for lobby {LobbyCode}", code);
                cts.Cancel();
                _timers.Remove(code);
            }
        }
    }

    private async Task<Lobby> GetLobby(string code)
        => await _repository.Get(code.ToUpperInvariant())
           ?? throw new InvalidOperationException($"Lobby {code} not found.");
}
