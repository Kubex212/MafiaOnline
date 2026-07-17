namespace MafiaOnline.Engine.Config;

/// <summary>
/// Top-level configuration for a game session, containing per-role settings.
/// </summary>
public class GameConfig
{
    public GuardianConfig Guardian { get; set; } = new();
    public DetectiveConfig Detective { get; set; } = new();
}
