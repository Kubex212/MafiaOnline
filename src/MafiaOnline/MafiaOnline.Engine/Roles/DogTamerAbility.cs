using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Dog tamer as a dog that will bark if there are exactly N werewolves in range.
/// The dog tamer will investigate the player and their neighbors, up to this range (range is per direction).
/// </summary>
public class DogTamerAbility(DogTamerConfig config) : NightAbility(25)
{
    public override bool IsAutomatic => true;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        // Dog Tamer examines up to 'Range' neighbors around a chosen target.
        // Historically this ability ignored explicit targets and used the actor's seat index
        // to pick a subject, so preserve that behaviour for now.

        var numWerewolves = 0;
        var range = config.Range;
        var seat = actor.SeatIndex;

        for (int offset = -range; offset <= range; offset++)
        {
            var idx = seat + offset;
            if (idx < 0 || idx >= alivePlayers.Count) continue;
            var p = alivePlayers[idx];
            if (p.Role.TeamId != 0) numWerewolves++;
        }

        context.DogTamerClues[actor.Id] = (numWerewolves == config.NumWerewolves);
    }
}
