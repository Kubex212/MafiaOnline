using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Seer (citizen) — each night targets two players and learns whether they are on the same team.
/// </summary>
public class Ranger : IRole, IHasNightAbility
{
    public Ranger()
    {
        NightAbility = new RangerTripwireAbility();
    }

    public RoleType Type => RoleType.Ranger;
    public Team Team => Team.Villagers;
    public string Name => "Ranger";
    public int TeamId => 0;

    public NightAbility NightAbility { get; }
}

public class RangerTripwireAbility() : NightAbility()
{
    protected override bool CanTargetSelf => true;
    protected override bool CanTargetDead => true;
    public override bool IsAutomatic => false;
    public override int MinTargets => 2;
    public override int MaxTargets => 2;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count < 2) return;
        var a = action.Targets[0];
        var b = action.Targets[1];

        context.TripwireVertices = (actor.Id, a, b);
    }
}
