using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MafiaOnline.Api.Services;

public interface IExternalAuthService
{
    /// <summary>
    /// Finds an existing user linked to this provider identity, or creates a new one.
    /// Returns the UserId to create a session for.
    /// </summary>
    Task<Guid> FindOrCreateUserAsync(string provider, string providerUserId);
}

public class ExternalAuthService : IExternalAuthService
{
    private readonly MafiaDbContext _db;

    public ExternalAuthService(MafiaDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> FindOrCreateUserAsync(string provider, string providerUserId)
    {
        var existing = await _db.ExternalLogins
            .FirstOrDefaultAsync(e => e.Provider == provider && e.ProviderUserId == providerUserId);

        if (existing is not null)
            return existing.UserId;

        // New user — create user + external login link
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var login = new ExternalLoginEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = provider,
            ProviderUserId = providerUserId
        };

        _db.Users.Add(user);
        _db.ExternalLogins.Add(login);
        await _db.SaveChangesAsync();

        return user.Id;
    }
}
