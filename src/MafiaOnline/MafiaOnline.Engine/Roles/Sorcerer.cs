using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public class Sorcerer : IRole, IHasNightAbility
{
    public Sorcerer()
    {
        NightAbility = new SorcererAbility();
    }

    public RoleType Type => RoleType.Sorcerer;
    public Team Team => Team.Villagers;
    public string Name => "Sorcerer";
    public NightAbility NightAbility { get; }
}

public sealed class SorcererAbility : NightAbility
{
    public SorcererAbility() : base() { }

    public override bool IsAutomatic => false;

    // one-use: long cooldown
    public override int? Cooldown => 999;

    // allow selecting as many targets as they wish
    public override int MinTargets => 1;
    public override int MaxTargets => int.MaxValue;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null) return;

        // record the selected targets and give the clue at resolution time
        var targets = action.Targets.Where(t => alivePlayers.Any(p => p.Id == t)).ToList();

        if (!targets.Any())
        {
            // still record empty selection so ability is considered used
            context.SorcererSelections[actor.Id] = new List<Guid>();
            return;
        }

        context.SorcererSelections[actor.Id] = targets;
    }
}
