using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public class Trailer : IRole, IHasNightAbility
{
    public Trailer()
    {
        NightAbility = new TrailerPassiveAbility();
    }

    public RoleType Type => RoleType.Trailer;
    public string Name => "Trailer";
    public int TeamId => 0;
    public Team Team => Team.Villagers;

    public NightAbility NightAbility { get; }
}
