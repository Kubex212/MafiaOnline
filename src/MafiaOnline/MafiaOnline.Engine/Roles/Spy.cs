using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Peeper (citizen) — each night may investigate a target to see whether they are villager or a neutral - however it they're a werewolf the peeper dies.
/// </summary>
public class Spy : IRole, IHasNightAbility
{
    public Spy()
    {
        NightAbility = new SpyInvestigateAbility();
    }

    public RoleType Type => RoleType.Spy;
    public Team Team => Team.Villagers;
    public string Name => "Spy";

    public NightAbility NightAbility { get; }
}

public sealed class SpyInvestigateAbility() : NightAbility()
{
    protected override bool CanTargetSelf => false;
    public override bool IsAutomatic => false;
    public override int MinTargets => 1;
    public override int MaxTargets => 1;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count != 1) return;
        var targetId = action.Targets[0];

        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId);
        if (target is null) return;

        context.SpyAbility = (actor.Id, target.Id);
    }
}
