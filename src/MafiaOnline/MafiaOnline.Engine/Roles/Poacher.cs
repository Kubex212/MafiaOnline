using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public class Poacher : IRole, IHasNightAbility
{
    public Poacher(PoacherConfig? config = null)
    {
        NightAbility = new PoacherTrapAbility(config ?? new PoacherConfig());
    }

    public RoleType Type => RoleType.Poacher;
    public string Name => "Poacher";
    public int TeamId => 0;
    public Team Team => Team.Villagers;

    public NightAbility NightAbility { get; }
}
