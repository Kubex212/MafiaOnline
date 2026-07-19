using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace MafiaOnline.Api.Services;

public interface IDayService
{
    Task BeginDayAsync(string lobbyCode);
    Task CastVoteAsync(string lobbyCode, Guid voterId, Guid candidateId);
}

public class DayService : IDayService
{
    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hub;
    private readonly Lazy<INightService> _nightService;
    private readonly Dictionary<string, CancellationTokenSource> _timers = new();

    public DayService(ILobbyRepository repository, IHubContext<LobbyHub> hub, Lazy<INightService> nightService)
    {
        _repository = repository;
        _hub = hub;
        _nightService = nightService;
    }

    public async Task BeginDayAsync(string lobbyCode)
    {
        var lobby = GetLobby(lobbyCode);
        lobby.BeginDaySession();
        _repository.Update(lobby);

        int duration = lobby.Game!.Config.Day.DurationSeconds;
        bool showTargets = lobby.Game.Config.Day.ShowVoteTargets;

        // Tell all clients the day has started
        var alivePlayers = lobby.Players
            .Where(p => lobby.Game.Players.First(ep => ep.Id == p.Id).IsAlive)
            .Select(p => new { id = p.Id, nickname = p.Nickname, seatIndex = p.SeatIndex })
            .ToList();

        await _hub.Clients.Group(lobby.Code).SendAsync("DayStarted", new
        {
            durationSeconds = duration,
            showVoteTargets = showTargets,
            alivePlayers
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

    public async Task CastVoteAsync(string lobbyCode, Guid voterId, Guid candidateId)
    {
        var lobby = GetLobby(lobbyCode);

        if (lobby.DaySession is null)
            throw new InvalidOperationException("No active day session.");

        bool alreadyVoted = lobby.DaySession.HasVoted(voterId);
        var votes = lobby.CastVote(voterId, candidateId);
        _repository.Update(lobby);

        bool showTargets = lobby.Game!.Config.Day.ShowVoteTargets;

        // Always broadcast that the voter has voted; conditionally include target
        await _hub.Clients.Group(lobby.Code).SendAsync("VoteCast", new
        {
            voterId,
            candidateId = showTargets ? (Guid?)candidateId : null,
            totalVotesCast = votes.Count
        });

        // If everyone has voted, resolve early
        if (lobby.DaySession.AllVoted)
        {
            CancelTimer(lobbyCode);
            await ResolveDayAsync(lobbyCode);
        }
    }

    // ?? internals ??????????????????????????????????????????????????????????

    private async Task ResolveDayAsync(string lobbyCode)
    {
        var lobby = GetLobby(lobbyCode);

        if (lobby.DaySession is null) return; // already resolved (timer race)

        var result = lobby.ResolveDay();
        _repository.Update(lobby);

        var banishedNames = result.Banished
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

        // Check game over
        if (lobby.Game!.Phase == global::MafiaOnline.Engine.GamePhase.Finished)
        {
            await _hub.Clients.Group(lobby.Code).SendAsync("GameOver", new
            {
                winner = lobby.Game.Winner.ToString()
            });
            return;
        }

        // Transition back to night after a pause
        await Task.Delay(5000);
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

    private Lobby GetLobby(string code)
        => _repository.Get(code.ToUpperInvariant())
           ?? throw new InvalidOperationException("Lobby not found.");
}
