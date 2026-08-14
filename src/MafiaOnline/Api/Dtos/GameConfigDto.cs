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
    public WerewolfConfig Werewolf { get; set; } = new();
    public GhostConfig Ghost { get; set; } = new();
    public DrunkConfig Drunk { get; set; } = new();

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
                Peeper = config.Roles.Peeper,
                Spy = config.Roles.Spy,
                Fool = config.Roles.Fool,
                Dracula = config.Roles.Dracula,
                Deceiver = config.Roles.Deceiver,
                Ghost = config.Roles.Ghost,
                Charmer = config.Roles.Charmer,
            },
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
            Roles = new RoleSelectionConfig
            {
                Guardian = Roles.Guardian,
                Detective = Roles.Detective,
                DogTamer = Roles.DogTamer,
                Poacher = Roles.Poacher,
                Trailer = Roles.Trailer,
                Seer = Roles.Seer,
                Peeper = Roles.Peeper,
                Spy = Roles.Spy,
                Fool = Roles.Fool,
                Dracula = Roles.Dracula,
                Deceiver = Roles.Deceiver,
                Ghost = Roles.Ghost,
                Charmer = Roles.Charmer,
            },
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

public class RoleSelectionDto
{
    public bool Guardian { get; set; } = true;
    public bool Detective { get; set; } = true;
    public bool DogTamer { get; set; } = true;
    public bool Poacher { get; set; } = true;
    public bool Trailer { get; set; } = true;
    public bool Seer { get; set; } = true;
    public bool Peeper { get; set; } = true;
    public bool Spy { get; set; } = true;
    public bool Fool { get; set; } = true;
    public bool Dracula { get; set; } = true;
    public bool Deceiver { get; set; } = true;
    public bool Ghost { get; set; } = true;
    public bool Charmer { get; set; } = true;
}
