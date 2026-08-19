using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Roles;

namespace MafiaOnline.Api.Dtos;

public class GameConfigDto
{
    public HashSet<RoleType> EnabledRoles { get; set; } = new();
    public int WerewolfCount { get; set; } = 1;
    public int TurnDurationSeconds { get; set; } = 30;
    public GuardianConfig Guardian { get; set; } = new();
    public DetectiveConfig Detective { get; set; } = new();
    public DogTamerConfig DogTamer { get; set; } = new();
    public DayConfig Day { get; set; } = new();
    public PoacherConfig Poacher { get; set; } = new();
    public WerewolfConfig Werewolf { get; set; } = new();
    public GhostConfig Ghost { get; set; } = new();
    public DrunkConfig Drunk { get; set; } = new();

    public static GameConfigDto FromConfig(GameConfig config)
    {
        return new GameConfigDto
        {
            EnabledRoles = config.EnabledRoles,
            WerewolfCount = config.WerewolfCount,
            TurnDurationSeconds = config.TurnDurationSeconds,
            Guardian = config.Guardian,
            Detective = config.Detective,
            DogTamer = config.DogTamer,
            Day = config.Day,
            Poacher = config.Poacher,
            Werewolf = config.Werewolf,
            Ghost = config.Ghost,
            Drunk = config.Drunk
        };
    }

    public GameConfig ToConfig()
    {
        return new GameConfig
        {
            EnabledRoles = EnabledRoles,
            WerewolfCount = WerewolfCount,
            TurnDurationSeconds = TurnDurationSeconds,
            Guardian = Guardian,
            Detective = Detective,
            DogTamer = DogTamer,
            Day = Day,
            Poacher = Poacher,
            Werewolf = Werewolf,
            Ghost = Ghost,
            Drunk = Drunk,
        };
    }
}
