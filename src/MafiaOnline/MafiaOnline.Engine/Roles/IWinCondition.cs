namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Roles implementing custom win logic should implement this interface.
/// Evaluate should return true when the role owner's win condition is met.
/// </summary>
public interface IWinCondition
{
    bool Evaluate(Game game, Player roleOwner);
}
