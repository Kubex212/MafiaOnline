using MafiaOnline.Api.Repositories;
using MafiaOnline.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static MafiaOnline.Engine.Game;

namespace Api.Services;

/// <summary>
/// Background service that periodically removes abandoned or finished lobbies.
///
/// Rules:
///   - Waiting (not-started) lobbies with no activity for ≥ 1 hour → deleted.
///   - Started lobbies whose game is finished and have had no activity for ≥ 1 hour → deleted.
///   - Active (in-progress) games are never touched regardless of their activity timestamp.
/// </summary>
public class LobbyCleanupService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InactivityCutoff = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LobbyCleanupService> _logger;

    public LobbyCleanupService(IServiceScopeFactory scopeFactory, ILogger<LobbyCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LobbyCleanupService started. Interval={Interval} Cutoff={Cutoff}", CheckInterval, InactivityCutoff);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error during lobby cleanup");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILobbyRepository>();

        var cutoff = DateTime.UtcNow.Subtract(InactivityCutoff);
        var candidates = await repository.GetInactiveAsync(cutoff);

        if (candidates.Count == 0)
        {
            _logger.LogDebug("LobbyCleanup: no inactive lobbies found");
            return;
        }

        var deleted = 0;
        foreach (var lobby in candidates)
        {
            if (ct.IsCancellationRequested) break;

            // Never delete a lobby with an active in-progress game
            if (lobby.Started && lobby.CurrentGame?.Phase != GamePhase.Finished)
            {
                _logger.LogDebug("LobbyCleanup: skipping lobby {Code} — game still in progress", lobby.Code);
                continue;
            }

            try
            {
                await repository.Delete(lobby.Code);
                deleted++;
                _logger.LogInformation(
                    "LobbyCleanup: deleted lobby {Code} (started={Started}, lastActivity={LastActivity:u})",
                    lobby.Code, lobby.Started, lobby.LastActivityAtUtc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LobbyCleanup: failed to delete lobby {Code}", lobby.Code);
            }
        }

        if (deleted > 0)
            _logger.LogInformation("LobbyCleanup: deleted {Count} inactive lobbies", deleted);
    }
}
