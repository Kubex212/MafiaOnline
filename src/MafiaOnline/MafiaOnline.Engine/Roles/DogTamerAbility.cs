using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Dog tamer as a dog that will bark if there are exactly N werewolves in range.
/// The dog tamer will investigate the player and their neighbors, up to this range (range is per direction).
/// </summary>
public class DogTamerAbility(DogTamerConfig config) : NightAbility(30)
{
    public override bool IsAutomatic => true;

    public override void Apply(Player actor, Guid? targetId, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        // neighbors: alive players other than the dog tamer himself, within the range
        var candidates = alivePlayers
            .Where(p => p.Id != actor.Id)
            .Where(p => Math.Abs(p.SeatIndex - actor.SeatIndex) <= config.Range)
            .ToList();

        if (candidates.Count == 0) return;

        // Count the number of werewolves in range
        var numWerewolvesInRange = candidates.Count(p => p.Role.TeamId == 1);
        var shouldBark = numWerewolvesInRange == config.NumWerewolves;

        context.DogTamerClues[actor.Id] = shouldBark;
    }
}
