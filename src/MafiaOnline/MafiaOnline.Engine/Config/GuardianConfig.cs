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
    public bool CanProtectSelf { get; set; } = false;

    /// <summary>
    /// Whether the Guardian can protect the same player on consecutive nights.
    /// Default: true.
    /// </summary>
    public bool CanProtectSamePlayerConsecutively { get; set; } = true;
}
