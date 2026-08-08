using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Roles;

namespace MafiaOnline.Engine.Core;

public static class RoleFactory
{
    public static IRole Create(RoleType type, GameConfig config)
    {
        return type switch
        {
            RoleType.Villager => new Villager(),
            RoleType.Guardian => new Guardian(config.Guardian),
            RoleType.Detective => new Detective(config.Detective),
            RoleType.DogTamer => new DogTamer(config.DogTamer),
            RoleType.Poacher => new Poacher(config.Poacher),
            RoleType.Trailer => new Trailer(),
            RoleType.Seer => new Seer(),

            RoleType.Dracula => new Dracula(),
            RoleType.Deceiver => new Deceiver(),
            RoleType.Ghost => new Ghost(config.Ghost),

            RoleType.Fool => new Fool(),

            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
