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
public class Peeper : IRole, IHasNightAbility
{
    private readonly DetectiveConfig _config;

    public Peeper()
    {
        NightAbility = new PeeperInvestigateAbility();
    }

    public RoleType Type => RoleType.Peeper;
    public Team Team => Team.Villagers;
    public string Name => "Peeper";
    public int TeamId => 0;

    public NightAbility NightAbility { get; }
}

public sealed class PeeperInvestigateAbility() : NightAbility()
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

        if (target.Role.VisibleTeam == Team.Werewolves)
        {
            context.PeeperKills.Add(actor.Id);
        }
        else
        {
            var isVillager = target.Role.VisibleTeam == Team.Villagers;
            context.AddEvent(new PeeperClueEvent(actor.Id, targetId, isVillager));
        }
    }
}
