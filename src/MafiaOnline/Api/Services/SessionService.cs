using System.Security.Cryptography;
using System.Text;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MafiaOnline.Api.Services;

public class SessionService : ISessionService
{
    private static readonly TimeSpan GuestExpiry = TimeSpan.FromDays(30);

    private readonly MafiaDbContext _db;

    public SessionService(MafiaDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> ValidateAsync(string rawToken)
    {
        var hash = Hash(rawToken);
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.TokenHash == hash && s.ExpiresAtUtc > DateTime.UtcNow);

        if (session is null) return null;

        session.LastUsedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return session.UserId;
    }

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
