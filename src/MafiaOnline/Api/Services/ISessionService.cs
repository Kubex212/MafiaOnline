namespace MafiaOnline.Api.Services;

public record SessionResult(Guid UserId, string Token, bool IsNew);

public interface ISessionService
{
    /// <summary>
    /// Validates an existing session token.
    /// Returns null if the token is invalid or expired.
    /// </summary>
    Task<Guid?> ValidateAsync(string rawToken);

    /// <summary>
    /// Creates a new anonymous user + session and returns the raw token.
    /// </summary>
    Task<SessionResult> CreateAsync();

    /// <summary>
    /// Creates a new session for an existing user and returns the raw token.
    /// </summary>
    Task<SessionResult> CreateForUserAsync(Guid userId);
}
