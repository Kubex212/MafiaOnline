using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Deceiver - selects a target to receive false information due to their role. E.g. trailer will receive opposite direcation that they should.
/// </summary>
public class Deceiver : IRole, IHasNightAbility
{
    public RoleType Type => RoleType.Deceiver;
    public Team Team => Team.Werewolves;
    public string Name => "Deceiver";
    public int TeamId => 1;

    public NightAbility NightAbility { get; } = new DeceiverAbility();
}

public class DeceiverAbility : NightAbility
{
    public DeceiverAbility() : base() { }

    public override bool IsAutomatic => false;

    public override int MinTargets => 1;
    public override int MaxTargets => 1;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count == 0) return;
        var targetId = action.Targets[0];
        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId);
        if (target is null) return;

        context.AddEvent(new DeceiverEvent(actor.Id, targetId));
    }
}
