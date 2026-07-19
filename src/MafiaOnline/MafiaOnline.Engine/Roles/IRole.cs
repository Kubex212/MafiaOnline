using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public enum RoleType
{
    Villager,
    Werewolf,
    Guardian,
    Detective
}

public interface IRole
{
    RoleType Type { get; }

    string Name { get; }

    /// <summary>
    /// 0 = Town, 1 = Werewolves.
    /// </summary>
    int TeamId { get; }
}