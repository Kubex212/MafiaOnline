using MafiaOnline.Engine.Config;

namespace MafiaOnline.Api.Dtos;

public class GameConfigDto
{
    public RoleSelectionDto Roles { get; set; } = new();
    public int WerewolfCount { get; set; } = 1;
    public int TurnDurationSeconds { get; set; } = 30;
    public GuardianConfig Guardian { get; set; } = new();
    public DetectiveConfig Detective { get; set; } = new();
    public DogTamerConfig DogTamer { get; set; } = new();
    public DayConfig Day { get; set; } = new();
    public PoacherConfig Poacher { get; set; } = new();

    public static GameConfigDto FromConfig(GameConfig config)
    {
        return new GameConfigDto
        {
            Roles = new RoleSelectionDto
            {
                Guardian = config.Roles.Guardian,
                Detective = config.Roles.Detective,
                DogTamer = config.Roles.DogTamer,
                Poacher = config.Roles.Poacher,
                Trailer = config.Roles.Trailer,
                Seer = config.Roles.Seer,
                Fool = config.Roles.Fool,
                Dracula = config.Roles.Dracula
            },
            WerewolfCount = config.WerewolfCount,
            TurnDurationSeconds = config.TurnDurationSeconds,
            Guardian = config.Guardian,
            Detective = config.Detective,
            DogTamer = config.DogTamer,
            Day = config.Day,
            Poacher = config.Poacher,
        };
    }

    public GameConfig ToConfig()
    {
        return new GameConfig
        {
            Roles = new RoleSelectionConfig
            {
                Guardian = Roles.Guardian,
                Detective = Roles.Detective,
                DogTamer = Roles.DogTamer,
                Poacher = Roles.Poacher,
                Trailer = Roles.Trailer,
                Seer = Roles.Seer,
                Fool = Roles.Fool,
                Dracula = Roles.Dracula
            },
            WerewolfCount = WerewolfCount,
            TurnDurationSeconds = TurnDurationSeconds,
            Guardian = Guardian,
            Detective = Detective,
            DogTamer = DogTamer,
            Day = Day,
            Poacher = Poacher,
        };
    }
}

public class RoleSelectionDto
{
    public bool Guardian { get; set; } = true;
    public bool Detective { get; set; } = true;
    public bool DogTamer { get; set; } = true;
    public bool Poacher { get; set; } = true;
    public bool Trailer { get; set; } = true;
    public bool Seer { get; set; } = false;
    public bool Fool { get; set; } = true;
    public bool Dracula { get; set; } = true;
}
