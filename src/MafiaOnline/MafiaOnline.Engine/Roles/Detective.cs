using MafiaOnline.Engine.Config;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Detective (citizen) — each night is automatically pointed at a random living player
/// (other than themselves) and receives their alignment (innocent / werewolf).
/// </summary>
public class Detective : IRole, IHasNightAbility
{
    public Detective(DetectiveConfig? config = null)
    {
        NightAbility = new DetectiveInvestigateAbility(config ?? new DetectiveConfig());
    }

    public RoleType Type => RoleType.Detective;
    public Team Team => Team.Villagers;
    public string Name => "Detective";
    public int TeamId => 0;

    public NightAbility NightAbility { get; }
}