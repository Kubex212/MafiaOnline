using MafiaOnline.Engine.Config;

namespace MafiaOnline.Engine.Config;

/// <summary>
/// Top-level configuration for a game session, containing per-role settings.
/// </summary>
public class GameConfig
{
    public RoleSelectionConfig Roles { get; set; } = new();
    public int WerewolfCount { get; set; } = 1;
    public int TurnDurationSeconds { get; set; } = 30;
    public GuardianConfig Guardian { get; set; } = new();
    public DetectiveConfig Detective { get; set; } = new();
    public DogTamerConfig DogTamer { get; set; } = new();
    public DayConfig Day { get; set; } = new();
    public PoacherConfig Poacher { get; set; } = new();
    public GhostConfig Ghost { get; set; } = new();

    public WerewolfConfig Werewolf { get; set; } = new();// Configuration for werewolf team behaviour (shared kill vote etc.)
    public static GameConfig GetDefault()
    {
        return new GameConfig
        {
            Roles = new RoleSelectionConfig
            {
                Guardian = true,
                Detective = true,
                DogTamer = true,
                Poacher = true,
                Trailer = true,
                Seer = true,
                Fool = true,
                Dracula = true,
                Deceiver = true,
                Ghost = true
            },
            WerewolfCount = 1,
            TurnDurationSeconds = 30,
            Guardian = new GuardianConfig(),
            Detective = new DetectiveConfig(),
            DogTamer = new DogTamerConfig(),
            Day = new DayConfig(),
            Poacher = new PoacherConfig(),
            Werewolf = new WerewolfConfig(),
            Ghost = new GhostConfig()
        };
    }
}
