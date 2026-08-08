using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

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
        return roleType switch
        {
            RoleType.Villager => Team.Villagers,
            RoleType.Guardian => Team.Villagers,
            RoleType.Detective => Team.Villagers,
            RoleType.DogTamer => Team.Villagers,
            RoleType.Poacher => Team.Villagers,
            RoleType.Trailer => Team.Villagers,
            RoleType.Seer => Team.Villagers,
            RoleType.Peeper => Team.Villagers,

            RoleType.Werewolf => Team.Werewolves,
            RoleType.Dracula => Team.Werewolves,
            RoleType.Deceiver => Team.Werewolves,
            RoleType.Ghost => Team.Werewolves,
            RoleType.Charmer => Team.Werewolves,

            RoleType.Fool => Team.Neutral,
            _ => throw new ArgumentOutOfRangeException(nameof(roleType), $"Unhandled role type: {roleType}")
        };
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