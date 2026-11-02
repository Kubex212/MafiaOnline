using StackExchange.Redis;

namespace MafiaOnline.Api.Services;

public record NightTimer(string LobbyCode, Guid GameId);

/// <summary>
/// Shared record of "this night must be resolved by time T". Lives outside any request scope
/// so a different request (or a restarted instance) can cancel or fire a timer.
/// </summary>
public interface INightTimerStore
{
    /// <summary>Registers (or replaces) the timer for a game's current night.</summary>
    Task ScheduleAsync(string lobbyCode, Guid gameId, TimeSpan delay);

    /// <summary>Removes the timer, e.g. because every player acted before the deadline.</summary>
    Task CancelAsync(string lobbyCode, Guid gameId);

    /// <summary>
    /// Removes and returns every timer whose deadline has passed. A timer is returned to exactly
    /// one caller, so several app instances can poll without resolving the same night twice.
    /// </summary>
    Task<IReadOnlyList<NightTimer>> ClaimDueAsync();
}

public class RedisNightTimerStore : INightTimerStore
{
    // Sorted set: member = "{lobbyCode}:{gameId}", score = deadline in unix milliseconds.
    private const string TimersKey = "mafia:night-timers";

    private readonly IDatabase _redis;

    public RedisNightTimerStore(IConnectionMultiplexer connection)
    {
        _redis = connection.GetDatabase();
    }

    public Task ScheduleAsync(string lobbyCode, Guid gameId, TimeSpan delay)
    {
        var deadline = DateTimeOffset.UtcNow.Add(delay).ToUnixTimeMilliseconds();
        return _redis.SortedSetAddAsync(TimersKey, ToMember(lobbyCode, gameId), deadline);
    }

    public Task CancelAsync(string lobbyCode, Guid gameId)
        => _redis.SortedSetRemoveAsync(TimersKey, ToMember(lobbyCode, gameId));

    public async Task<IReadOnlyList<NightTimer>> ClaimDueAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var due = await _redis.SortedSetRangeByScoreAsync(TimersKey, stop: now);

        var claimed = new List<NightTimer>();
        foreach (var member in due)
        {
            // ZREM only reports success to one caller, which makes that caller the owner of the timer.
            if (await _redis.SortedSetRemoveAsync(TimersKey, member))
                claimed.Add(FromMember(member.ToString()));
        }

        return claimed;
    }

    private static string ToMember(string lobbyCode, Guid gameId)
        => $"{lobbyCode.ToUpperInvariant()}:{gameId}";

    private static NightTimer FromMember(string member)
    {
        var parts = member.Split(':');
        return new NightTimer(parts[0], Guid.Parse(parts[1]));
    }
}
