using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Fool (neutral) — wins if they get banished during the day.
/// </summary>
public class Fool : IRole, IWinCondition
{
    public RoleType Type => RoleType.Villager; // neutral type placeholder
    public string Name => "Fool";
    public int TeamId => 2; // neutral

    public bool Evaluate(Game game, Player roleOwner)
    {
        var lastDay = game.LastDayResult;
        if (lastDay is null) return false;
        return lastDay.Banished.Contains(roleOwner.Id);
    }
}
