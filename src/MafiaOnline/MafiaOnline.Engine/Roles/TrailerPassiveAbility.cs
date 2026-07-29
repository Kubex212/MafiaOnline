using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public class TrailerPassiveAbility() : NightAbility()
{
    public override bool IsAutomatic => true;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        // find nearest werewolf in both directions around the circular table
        var myIndex = Enumerable.Range(0, alivePlayers.Count).First(i => alivePlayers[i].Id == actor.Id);

        var n = alivePlayers.Count;

        var bestLeft = int.MaxValue;
        var bestRight = int.MaxValue;

        for (var i = 0; i < n; i++)
        {
            var p = alivePlayers[i];

            if (p.Id == actor.Id)
                continue;

            if (p.Role.TeamId != 1)
                continue;

            var clockwise = (i - myIndex + n) % n;
            var counterClockwise = (myIndex - i + n) % n;

            if (clockwise < bestLeft)
                bestLeft = clockwise;

            if (counterClockwise < bestRight)
                bestRight = counterClockwise;
        }

        string dir;
        if (bestLeft == int.MaxValue && bestRight == int.MaxValue)
        {
            // no werewolves alive
            dir = "None";
        }
        else if (bestLeft < bestRight) dir = "Left";
        else if (bestRight < bestLeft) dir = "Right";
        else
        {
            // equal distance — randomize
            dir = (new Random().Next(2) == 0) ? "Left" : "Right";
        }

        context.TrailerClues[actor.Id] = dir;
    }
}
