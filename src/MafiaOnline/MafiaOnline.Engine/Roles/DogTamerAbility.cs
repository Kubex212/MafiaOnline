using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Dog tamer as a dog that will bark if there are exactly N werewolves in range.
/// The dog tamer will investigate the player and their neighbors, up to this range (range is per direction).
/// So if the range is 2 and N is 1, the dog tamer will bark if there is exactly 1 werewolf in the 5 players (themselves and 2 on each side).
/// </summary>
public class DogTamerAbility(DogTamerConfig config) : NightAbility()
{
    public override bool IsAutomatic => true;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {

        var numWerewolves = 0;
        var range = config.Range;
        var seat = actor.SeatIndex;

        for (int offset = -range; offset <= range; offset++)
        {
            var idx = seat + offset;
            if (idx < 0 || idx >= alivePlayers.Count) continue;
            var p = alivePlayers[idx];
            if (p.Role.VisibleTeam == Team.Werewolves) numWerewolves++;
        }

        context.DogTamerClues[actor.Id] = (numWerewolves == config.NumWerewolves);
    }
}
