using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Detective ability — automatically points at a random living non-detective citizen
/// and records the clue.
/// Priority 30: fires last so the detective's information reflects the current state
/// (e.g. whether someone died tonight).
/// </summary>
public class DetectiveInvestigateAbility(DetectiveConfig config) : NightAbility()
{
    public override bool IsAutomatic => true;
    public override int MinTargets => 0;
    public override int MaxTargets => 0;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        // Candidates: alive villagers other than the detective himself
        var villagerCandidates = alivePlayers
            .Where(p => p.Id != actor.Id && p.Role.VisibleTeam == Team.Villagers)
            .ToList();

        if (villagerCandidates.Count == 0) return;

        // Pick a random candidate
        var random = new Random();
        var subject = villagerCandidates[random.Next(villagerCandidates.Count)];

        // Candidates: alive werewolves (possibility in case the detective is a deceiver's target)
        var werewolfCandidates = alivePlayers
            .Where(p => p.Role.VisibleTeam == Team.Werewolves)
            .ToList();

        Player werewolf;
        if (werewolfCandidates.Count == 0) // in case there is only a Ghost
        {
            werewolf = subject;
        }
        else
        {
            werewolf = werewolfCandidates[random.Next(werewolfCandidates.Count)];
        }

        context.DetectiveClue = new DetectiveClue()
        {
            DetectiveId = actor.Id,
            VillagerId = subject.Id,
            WerewolfId = werewolf.Id
        };
    }
}

public class DetectiveClue
{ 
    public Guid DetectiveId { get; set; }
    public Guid VillagerId { get; set; }
    public Guid WerewolfId { get; set; }
}
