using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Config;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Werewolf ability — each werewolf may submit a vote for a target. Votes are resolved collectively by NightResolver.
/// Priority 20: fires after Guardian (10) but before Detective (30).
/// </summary>
public class WerewolfAttackAbility : NightAbility
{
    public WerewolfAttackAbility() : base(20) { }

    public override bool IsAutomatic => false;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count == 0) return;
        var targetId = action.Targets[0];
        // Only allow attacking living players who are not on werewolf team
        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId && p.Role.TeamId != actor.Role.TeamId);
        if (target is null) return;

        context.WerewolfAttacks[actor.Id] = targetId;
    }

    public override void Apply(Player actor, Guid? targetId, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        base.Apply(actor, targetId, context, alivePlayers);
    }
}
