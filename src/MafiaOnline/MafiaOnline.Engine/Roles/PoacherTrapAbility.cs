using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public class PoacherTrapAbility(PoacherConfig config) : NightAbility(12)
{
    public override bool IsAutomatic => false;

    public override int MinTargets => 1;
    public override int MaxTargets => 1;
    public override bool CanTargetSelf => config.AllowSelfTarget;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count == 0) return;
        var targetId = action.Targets[0];

        // validate target is alive
        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId);
        if (target is null) return;

        // enforce self-target config
        if (!CanTargetSelf && actor.Id == targetId) return;

        // enforce must-alternate
        if (config.MustAlternate && actor.LastPoacherTrapTarget == targetId)
        {
            // ignore repeating trap
            return;
        }

        context.PoacherTraps[actor.Id] = targetId;

        // persist last trap target for alternating behaviour
        actor.LastPoacherTrapTarget = targetId;
    }
}
