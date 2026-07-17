using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Werewolf ability — marks a target player for death.
/// Priority 20: fires after Guardian (10) but before Detective (30).
/// </summary>
public class WerewolfAttackAbility() : NightAbility(20)
{
    public override bool IsAutomatic => false;

    public override void Apply(Player actor, Guid? targetId, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (targetId is null) return;
        // Only allow attacking living players who are not werewolves
        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId && p.Role.TeamId != actor.Role.TeamId);
        if (target is null) return;

        context.WerewolfAttacks[actor.Id] = targetId.Value;
    }
}
