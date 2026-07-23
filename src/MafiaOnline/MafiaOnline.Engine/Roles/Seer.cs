using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Seer (citizen) — each night targets two players and learns whether they are on the same team.
/// </summary>
public class Seer : IRole, IHasNightAbility
{
    private readonly DetectiveConfig _config;

    public Seer()
    {
        NightAbility = new SeerInvestigateAbility();
    }

    public RoleType Type => RoleType.Detective; // reuse detective type or add new RoleType if desired
    public string Name => "Seer";
    public int TeamId => 0;

    public NightAbility NightAbility { get; }
}
