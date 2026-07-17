namespace MafiaOnline.Engine.Commands;

public abstract class GameCommand
{
    protected GameCommand(Guid playerId)
    {
        PlayerId = playerId;
    }

    public Guid PlayerId { get; }

    internal abstract void Execute(Game game);
}

public class AddPlayerCommand : GameCommand
{
    public AddPlayerCommand(Guid playerId, string nickname)
        : base(playerId)
    {
        Nickname = nickname;
    }

    public string Nickname { get; }

    internal override void Execute(Game game)
    {
        if (game.Status != GameStatus.WaitingForPlayers)
            throw new InvalidOperationException("Game already started.");

        game.PlayersInternal.Add(new Player(PlayerId, Nickname));
    }
}

public class StartGameCommand : GameCommand
{
    public StartGameCommand(Guid playerId)
        : base(playerId)
    {
    }

    internal override void Execute(Game game)
    {
        if (game.Players.Count < 4)
            throw new InvalidOperationException("Not enough players (minimum 4).");

        game.AssignRoles();

        game.Status = GameStatus.InProgress;
        game.Phase = GamePhase.Night;
    }
}

/// <summary>
/// Submitted by a player to declare their night action target.
/// </summary>
public class NightActionCommand : GameCommand
{
    public NightActionCommand(Guid playerId, Guid targetId)
        : base(playerId)
    {
        TargetId = targetId;
    }

    public Guid TargetId { get; }

    internal override void Execute(Game game)
    {
        game.SubmitNightAction(PlayerId, TargetId);
    }
}