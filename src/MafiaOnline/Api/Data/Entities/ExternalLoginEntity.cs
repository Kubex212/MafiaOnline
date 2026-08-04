namespace Api.Data.Entities;

public class ExternalLoginEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>e.g. "Discord"</summary>
    public string Provider { get; set; } = null!;

    /// <summary>The user's ID on the external provider.</summary>
    public string ProviderUserId { get; set; } = null!;

    public UserEntity User { get; set; } = null!;
}
