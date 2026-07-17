using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Guardian ability — registers a protection on a target player.
/// Priority 10: fires first so the protection is in place when attacks are resolved.
/// </summary>
public class GuardianProtectAbility(GuardianConfig config, Player guardianPlayer) : NightAbility(10)
{
    public override bool IsAutomatic => false;

    public override void Apply(Player actor, Guid? targetId, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (targetId is null) return;

        if (!config.CanProtectSelf && targetId == actor.Id) return;

        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId);
        if (target is null) return;

        context.GuardianProtections[actor.Id] = targetId.Value;
    }
}
