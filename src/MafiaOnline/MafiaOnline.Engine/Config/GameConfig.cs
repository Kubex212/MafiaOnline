using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Roles;
using Newtonsoft.Json;

namespace MafiaOnline.Engine.Config;

/// <summary>
/// Top-level configuration for a game session, containing per-role settings.
/// </summary>
public class GameConfig
{
    public int WerewolfCount { get; set; } = 1;
    public int TurnDurationSeconds { get; set; } = 30;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<RoleType> EnabledRoles { get; set; } = new();
    public GuardianConfig Guardian { get; set; } = new();
    public DetectiveConfig Detective { get; set; } = new();
    public DogTamerConfig DogTamer { get; set; } = new();
    public DayConfig Day { get; set; } = new();
    public PoacherConfig Poacher { get; set; } = new();
    public GhostConfig Ghost { get; set; } = new();
    public DrunkConfig Drunk { get; set; } = new();
    public WerewolfConfig Werewolf { get; set; } = new();

    public GameConfig()
    {
        EnabledRoles = GetDefaultRoleSet();
    }

    public static GameConfig GetDefault()
    {
        return new GameConfig
        {
            EnabledRoles = GetDefaultRoleSet(),
            WerewolfCount = 1,
            TurnDurationSeconds = 30,
            Guardian = new GuardianConfig(),
            Detective = new DetectiveConfig(),
            DogTamer = new DogTamerConfig(),
            Day = new DayConfig(),
            Poacher = new PoacherConfig(),
            Werewolf = new WerewolfConfig(),
            Ghost = new GhostConfig(),
            Drunk = new DrunkConfig(),
        };
    }

    private static HashSet<RoleType> GetDefaultRoleSet()
    {
        var roles = Enum.GetValues<RoleType>().ToHashSet();
        roles.Remove(RoleType.Villager);
        roles.Remove(RoleType.Werewolf);
        return roles;
    }
}
