using Api.Services;
using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using Microsoft.AspNetCore.SignalR;
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
    private readonly Lazy<INightService> _nightService;
    private readonly Dictionary<string, CancellationTokenSource> _timers = new();
    private readonly IGameHistoryService _gameHistoryService;

    public DayService(ILobbyRepository repository, IHubContext<LobbyHub> hub, Lazy<INightService> nightService, IGameHistoryService gameHistoryService)
    {
        _repository = repository;
        _hub = hub;
        _nightService = nightService;
        _gameHistoryService = gameHistoryService;
    }

    public async Task BeginDayAsync(string lobbyCode)
    {
        var lobby = await GetLobby(lobbyCode);
        lobby.BeginDaySession();
        await _repository.Update(lobby);

        var duration = lobby.Game!.Config.Day.DurationSeconds;
        var showTargets = lobby.Game.Config.Day.ShowVoteTargets;

        // Determine enshrouded players from last night, if any
        var enshrouded = new List<Guid>();
        var last = lobby.Game?.LastNightResult;
        if (last != null)
        {
            foreach (var ev in last.Events)
            {
                if (ev is DraculaEnshroudedEvent d)
                    enshrouded.Add(d.TargetId);
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
            enshrouded = enshrouded
        });

        // Start day countdown timer
        var cts = new CancellationTokenSource();
        lock (_timers) { _timers[lobby.Code] = cts; }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(duration), cts.Token);
                await ResolveDayAsync(lobbyCode);
            }
            catch (TaskCanceledException) { /* all players voted early */ }
        }, CancellationToken.None);
    }

    public async Task CastVoteAsync(string lobbyCode, Guid userId, Guid? candidateId)
    {
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

        if (lobby.DaySession.AllVoted)
        {
            CancelTimer(lobbyCode);
            await ResolveDayAsync(lobbyCode);
        }
    }

    private async Task ResolveDayAsync(string lobbyCode)
    {
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

        // wait a few seconds before transitioning to night/gameover so players can see the results
        await Task.Delay(5000);

        // Check game over
        if (lobby.Game!.Phase == global::MafiaOnline.Engine.GamePhase.Finished)
        {
            await _gameHistoryService.RecordGameAsync(lobby);

            await _hub.Clients.Group(lobby.Code).SendAsync("GameOver", new
            {
                winner = lobby.Game.Winner.ToString()
            });
            return;
        }

        // transition back to night
        await _nightService.Value.BeginNightAsync(lobbyCode);
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
