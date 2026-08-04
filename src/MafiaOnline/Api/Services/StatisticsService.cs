using Api.Data;
using Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IStatisticsService
{
    Task<UserStatisticsResponse> GetStatisticsAsync(Guid userId);

    Task<IReadOnlyList<GameHistoryItemResponse>> GetRecentGamesAsync(Guid userId, int take = 20);
}

public class StatisticsService : IStatisticsService
{
    private readonly MafiaDbContext _db;

    public StatisticsService(MafiaDbContext db)
    {
        _db = db;
    }

    public async Task<UserStatisticsResponse> GetStatisticsAsync(Guid userId)
    {
        var games = await _db.GameHistory
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var gamesPlayed = games.Count;

        var wins = games.Count(x => x.Won);

        var losses = gamesPlayed - wins;

        var survivalRate = gamesPlayed == 0
            ? 0
            : games.Count(x => x.Survived) * 100.0 / gamesPlayed;

        var roles = games
            .GroupBy(x => x.Role)
            .ToDictionary(
                g => g.Key,
                g => new RoleStatisticsResponse
                {
                    Games = g.Count(),
                    Wins = g.Count(x => x.Won),
                    WinRate = g.Count() == 0
                        ? 0
                        : g.Count(x => x.Won) * 100.0 / g.Count()
                });

        return new UserStatisticsResponse
        {
            GamesPlayed = gamesPlayed,
            Wins = wins,
            Losses = losses,
            WinRate = gamesPlayed == 0 ? 0 : wins * 100.0 / gamesPlayed,
            SurvivalRate = survivalRate,
            Roles = roles
        };
    }

    public async Task<IReadOnlyList<GameHistoryItemResponse>> GetRecentGamesAsync(Guid userId, int take = 20)
    {
        return await _db.GameHistory
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.PlayedAtUtc)
            .Take(take)
            .Select(x => new GameHistoryItemResponse
            {
                PlayedAtUtc = x.PlayedAtUtc,
                Role = x.Role.ToString(),
                Won = x.Won,
                Survived = x.Survived,
                PlayerCount = x.PlayerCount,
                Duration = x.Duration
            })
            .ToListAsync();
    }
}
