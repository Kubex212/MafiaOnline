namespace Api.Data.Entities;

public class SessionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the raw token sent to the client.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastUsedAtUtc { get; set; }

    public UserEntity User { get; set; } = null!;
}
