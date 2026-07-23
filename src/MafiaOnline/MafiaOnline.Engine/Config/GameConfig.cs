using MafiaOnline.Engine.Config;

namespace MafiaOnline.Engine.Config;

/// <summary>
/// Top-level configuration for a game session, containing per-role settings.
/// </summary>
public class GameConfig
{
    public GuardianConfig Guardian { get; set; } = new();
    public DetectiveConfig Detective { get; set; } = new();
    public DogTamerConfig DogTamer { get; set; } = new();
    public DayConfig Day { get; set; } = new();
    public PoacherConfig Poacher { get; set; } = new();
}
