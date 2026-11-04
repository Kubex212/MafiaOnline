using MafiaOnline.Api.Repositories;
using StackExchange.Redis;

namespace MafiaOnline.Api.Services;

/// <summary>
/// Background service that resolves nights whose timer has run out.
///
/// Timers live in Redis (see <see cref="INightTimerStore"/>), so this also picks up timers that
/// expired while the app was down: they are overdue on the first poll and fire immediately.
/// </summary>
public class NightTimerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly INightTimerStore _timers;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NightTimerService> _logger;

    public NightTimerService(INightTimerStore timers, IServiceScopeFactory scopeFactory, ILogger<NightTimerService> logger)
    {
        _timers = timers;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NightTimerService started. Interval={Interval}", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var timer in await _timers.ClaimDueAsync())
                {
                    // Resolving a night waits several seconds before the day starts; don't hold up other timers.
                    _ = ResolveNightAsync(timer);
                }
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning("Redis is unavailable, night timers were not checked: {Message}", ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error while checking night timers");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ResolveNightAsync(NightTimer timer)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILobbyRepository>();
            var lobby = await repository.Get(timer.LobbyCode);

            if (lobby?.CurrentGame?.Id != timer.GameId || lobby.NightSession is null)
            {
                _logger.LogDebug("Night timer expired but there is no active night for lobby {LobbyCode} game={GameId}", timer.LobbyCode, timer.GameId);
                return;
            }

            _logger.LogInformation("Night timer expired for lobby {LobbyCode} game={GameId}, resolving night", timer.LobbyCode, timer.GameId);
            var nightService = scope.ServiceProvider.GetRequiredService<INightService>();
            await nightService.ResolveNightAsync(lobby);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving night for lobby {LobbyCode} game={GameId}", timer.LobbyCode, timer.GameId);
        }
    }
}
