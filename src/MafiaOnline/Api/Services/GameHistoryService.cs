using Api.Data;
using Api.Data.Entities;
using MafiaOnline.Api.Models;

namespace Api.Services;

public interface IGameHistoryService
{
    Task RecordGameAsync(Lobby lobby);
}

public class GameHistoryService : IGameHistoryService
{
    private readonly MafiaDbContext _db;

    public GameHistoryService(MafiaDbContext db)
    {
        _db = db;
    }

    public async Task RecordGameAsync(Lobby lobby)
    {
        var now = DateTime.UtcNow;

        foreach (var player in lobby.Players)
        {
            if (player.UserId == Guid.Empty)
                continue;

            var enginePlayer = lobby.Game!.Players.SingleOrDefault(p => p.Id == player.Id)!;
            _db.GameHistory.Add(new GameHistoryEntity
            {
                Id = Guid.NewGuid(),
                GameId = lobby.Id,
                UserId = player.UserId,

                PlayedAtUtc = now,

                Role = enginePlayer.Role.Type,
                TeamId = (int)enginePlayer.Role.Team,

                Won = (int)enginePlayer.Role.Team == (int)lobby.Game!.Winner,
                Survived = enginePlayer.IsAlive,

                PlayerCount = lobby.Players.Count,

                DayCount = lobby.Game.Turns?.Count ?? 0,

                Duration = now - lobby.Game.StartedAtUtc
            });
        }

        await _db.SaveChangesAsync();
    }
}
