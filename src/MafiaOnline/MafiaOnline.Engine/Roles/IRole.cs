using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public enum RoleType
{
    Villager,
    Werewolf,
    Guardian,
    Detective,
    DogTamer
}

public enum Team
{
    Town = 0,
    Werewolves = 1,
    Neutral = 2
}

public interface IRole
{
    RoleType Type { get; }

    string Name { get; }

    /// <summary>
    /// 0 = Town, 1 = Werewolves.
    /// </summary>
    int TeamId { get; }

    // New convenience property exposing a Team enum while remaining
    // backward-compatible with existing TeamId usages. Default
    // implementation converts TeamId to Team.
    Team Team => (Team)TeamId;
}