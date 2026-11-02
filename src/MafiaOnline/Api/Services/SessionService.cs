using System.Security.Cryptography;
using System.Text;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace MafiaOnline.Api.Services;

public class SessionService : ISessionService
{
    private static readonly TimeSpan GuestExpiry = TimeSpan.FromDays(30);

    // Cached sessions are dropped after this long without a request.
    private static readonly TimeSpan CacheSlidingExpiry = TimeSpan.FromMinutes(30);

    private readonly MafiaDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SessionService> _logger;

    public SessionService(MafiaDbContext db, IDistributedCache cache, ILogger<SessionService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Guid?> ValidateAsync(string rawToken)
    {
        var hash = Hash(rawToken);

        var cachedUserId = await TryGetCachedUserIdAsync(hash);
        if (cachedUserId.HasValue) return cachedUserId;

        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.TokenHash == hash && s.ExpiresAtUtc > DateTime.UtcNow);

        if (session is null) return null;

        // LastUsedAtUtc is only refreshed on a cache miss, so it is accurate to within the cache window.
        session.LastUsedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await TryCacheUserIdAsync(hash, session);

        return session.UserId;
    }

    // Redis is an optimisation: if it is unavailable, fall back to the database instead of failing the request.
    private async Task<Guid?> TryGetCachedUserIdAsync(string hash)
    {
        try
        {
            var value = await _cache.GetStringAsync(CacheKey(hash));
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session cache read failed, falling back to the database");
            return null;
        }
    }

    private async Task TryCacheUserIdAsync(string hash, SessionEntity session)
    {
        try
        {
            await _cache.SetStringAsync(CacheKey(hash), session.UserId.ToString(), new DistributedCacheEntryOptions
            {
                SlidingExpiration = CacheSlidingExpiry,
                // A cached session must never outlive the session itself.
                AbsoluteExpiration = new DateTimeOffset(DateTime.SpecifyKind(session.ExpiresAtUtc, DateTimeKind.Utc))
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session cache write failed");
        }
    }

    private static string CacheKey(string tokenHash) => $"session:{tokenHash}";

    public async Task<SessionResult> CreateAsync()
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var rawToken = GenerateToken();
        var session = new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(GuestExpiry),
            LastUsedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        return new SessionResult(user.Id, rawToken, IsNew: true);
    }

    public async Task<SessionResult> CreateForUserAsync(Guid userId)
    {
        var rawToken = GenerateToken();
        var session = new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(GuestExpiry),
            LastUsedAtUtc = DateTime.UtcNow
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();
        return new SessionResult(userId, rawToken, IsNew: false);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
