using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public class PoacherTrapAbility(PoacherConfig config) : NightAbility()
{
    protected override bool CanTargetSelf => config.AllowSelfTarget;
    protected override int TargetCooldown => config.MustAlternate ? 1 : 0;
    public override bool IsAutomatic => false;
    public override int MinTargets => 1;
    public override int MaxTargets => 1;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count == 0) return;
        var targetId = action.Targets[0];

        // validate target is alive
        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId);
        if (target is null) return;

        // enforce self-target config
        if (!CanTargetSelf && actor.Id == targetId) return;

        context.PoacherTraps[actor.Id] = targetId;
    }
}
