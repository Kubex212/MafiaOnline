using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Guardian ability — registers a protection on a target player.
/// Priority 10: fires first so the protection is in place when attacks are resolved.
/// </summary>
public class GuardianProtectAbility(GuardianConfig config) : NightAbility()
{
    public override bool IsAutomatic => false;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count == 0) return;
        var targetId = action.Targets[0];

        // Prevent self-protection if not allowed
        if (!config.CanProtectSelf && targetId == actor.Id) return;

        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId && p.IsAlive);
        if (target is null) return;

        context.GuardianProtections[actor.Id] = targetId;
    }

    public override void Apply(Player actor, Guid? targetId, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        base.Apply(actor, targetId, context, alivePlayers);
    }
}
