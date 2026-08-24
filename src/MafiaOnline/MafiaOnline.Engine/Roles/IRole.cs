using MafiaOnline.Engine.Core;
using MafiaOnline.Engine.Night;
using System.Text.Json.Serialization;

namespace MafiaOnline.Engine.Roles;

[JsonConverter(typeof(JsonStringEnumConverter))]

public enum RoleType
{
    Villager,
    Guardian,
    Detective,
    DogTamer,
    Poacher,
    Trailer,
    Seer,
    Peeper,
    Spy,
    Ranger,
    Alchemist,

    Werewolf,
    Dracula,
    Deceiver,
    Ghost,
    Charmer,

    Fool
}

public static class RoleTypeExtensions
{
    public static Team GetTeam(this RoleType roleType)
    {
        var role = RoleFactory.Create(roleType, new Config.GameConfig()); // using default is fine here, this object won't even use config
        return role.Team;
    }

    public static bool GivesNightClue(this RoleType role)
    {
        switch (role)
        {
            case RoleType.Detective:
            case RoleType.Seer:
            case RoleType.DogTamer:
            case RoleType.Trailer:
            case RoleType.Peeper:
            case RoleType.Spy:
            case RoleType.Ranger:
            case RoleType.Alchemist:
                return true;
            default:
                return false;
        }
    }
}

public enum Team
{
    Villagers = 0,
    Werewolves = 1,
    Neutral = 2
}

public interface IRole
{
    RoleType Type { get; }

    string Name { get; }

    Team Team { get; }
    Team VisibleTeam => Team;
}

public interface IWerewolfRole : IRole
{
    public RoleType? Alibi { get; set; }
}