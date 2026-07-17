using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Detective ability — automatically points at a random living non-detective citizen
/// and records the clue.
/// Priority 30: fires last so the detective's information reflects the current state
/// (e.g. whether someone died tonight).
/// </summary>
public class DetectiveInvestigateAbility(DetectiveConfig config) : NightAbility(30)
{
    public override bool IsAutomatic => true;

    public override void Apply(Player actor, Guid? targetId, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        // Candidates: alive players other than the detective himself
        var candidates = alivePlayers
            .Where(p => p.Id != actor.Id)
            .ToList();

        if (candidates.Count == 0) return;

        // Pick a random candidate
        var random = new Random();
        var subject = candidates[random.Next(candidates.Count)];

        bool isWerewolf = subject.Role.TeamId != 0;
        if (!config.CanRevealWerewolves && isWerewolf)
        {
            // Config says don't reveal werewolves — skip this night's clue
            return;
        }

        context.DetectiveClues[actor.Id] = subject.Id;
    }
}
