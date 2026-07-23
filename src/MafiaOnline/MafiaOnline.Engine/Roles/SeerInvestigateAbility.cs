using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Seer ability — targets two players and learns whether they're on the same team.
/// Priority 30: after attacks are resolved.
/// </summary>
public class SeerInvestigateAbility() : NightAbility(30)
{
    public override bool IsAutomatic => false;
    public override int MinTargets => 2;
    public override int MaxTargets => 2;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count < 2) return;
        var a = action.Targets[0];
        var b = action.Targets[1];

        var pa = alivePlayers.FirstOrDefault(p => p.Id == a);
        var pb = alivePlayers.FirstOrDefault(p => p.Id == b);
        if (pa is null || pb is null) return;

        bool sameTeam = pa.Role.TeamId == pb.Role.TeamId;
        context.AddEvent(new SeerClueEvent(actor.Id, a, b, sameTeam));
    }
}
