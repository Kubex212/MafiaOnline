namespace MafiaOnline.Engine.Config;

/// <summary>
/// Configuration options for the Guardian role.
/// </summary>
public class GuardianConfig
{
    /// <summary>
    /// Whether the Guardian is allowed to choose themselves as protection target.
    /// Default: false.
    /// </summary>
    public bool CanProtectSelf { get; set; } = true;

    /// <summary>
    /// Whether the Guardian must switch targets on consecutive nights.
    /// Default: true.
    /// </summary>
    public bool MustAlternate { get; set; } = true;

    /// <summary>
    /// Should the lobby be notified when the Guardian successfully protects a player from an attack?
    /// </summary>
    public bool AnnounceProtection { get; set; } = false;
}
