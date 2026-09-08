using Api.Data;
using Api.Data.Entities;
using MafiaOnline.Api.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Api.Services;

public interface IGameHistoryService
{
    Task RecordGameAsync(Lobby lobby);
    Task<IReadOnlyList<LobbyGameEntity>> GetLobbyGamesAsync(Guid lobbyId, int take = 20);
}

public class GameHistoryService : IGameHistoryService
{
    private readonly MafiaDbContext _db;
    private readonly JsonSerializerSettings _settings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        Converters = { new StringEnumConverter() },
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    public GameHistoryService(MafiaDbContext db)
    {
        _db = db;
    }

    public async Task RecordGameAsync(Lobby lobby)
    {
        var game = lobby.CurrentGame ?? throw new InvalidOperationException("Game not found.");
        var now = DateTime.UtcNow;

        var alreadyRecorded = _db.LobbyGames.Any(x => x.Id == game.Id);
        if (alreadyRecorded)
            return;

        _db.LobbyGames.Add(new LobbyGameEntity
        {
            Id = game.Id,
            LobbyId = lobby.Id,
            LobbyCode = lobby.Code,
            StartedAtUtc = game.StartedAtUtc,
            FinishedAtUtc = now,
            Winner = game.Winner.ToString(),
            PlayerCount = game.Players.Count,
            DayCount = game.Turns?.Count ?? 0,
            Duration = now - game.StartedAtUtc,
            ConfigJson = JsonConvert.SerializeObject(game.Config, _settings)
        });

        foreach (var player in lobby.Players)
        {
            if (player.UserId == Guid.Empty)
                continue;

            var enginePlayer = game.Players.SingleOrDefault(p => p.Id == player.Id);
            if (enginePlayer is null)
                continue;

            _db.GameHistory.Add(new GameHistoryEntity
            {
                Id = Guid.NewGuid(),
                UserId = player.UserId,
                LobbyId = lobby.Id,
                LobbyCode = lobby.Code,
                GameId = game.Id,
                PlayedAtUtc = now,
                Role = enginePlayer.Role.Type,
                TeamId = (int)enginePlayer.Role.Team,
                Won = (int)enginePlayer.Role.Team == (int)game.Winner,
                Survived = enginePlayer.IsAlive,
                PlayerCount = game.Players.Count,
                DayCount = game.Turns?.Count ?? 0,
                Duration = now - game.StartedAtUtc
            });
        }

        await _db.SaveChangesAsync();
    }

    public Task<IReadOnlyList<LobbyGameEntity>> GetLobbyGamesAsync(Guid lobbyId, int take = 20)
    {
        IReadOnlyList<LobbyGameEntity> result = _db.LobbyGames
            .Where(x => x.LobbyId == lobbyId)
            .OrderByDescending(x => x.FinishedAtUtc)
            .Take(take)
            .ToList();

        return Task.FromResult(result);
    }
}
