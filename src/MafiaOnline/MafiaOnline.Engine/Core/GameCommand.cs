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
    public StartGameCommand(Guid playerId, IReadOnlyList<Guid>? orderedPlayerIds = null)
        : base(playerId)
    {
        OrderedPlayerIds = orderedPlayerIds;
    }

    public IReadOnlyList<Guid>? OrderedPlayerIds { get; }

    internal override void Execute(Game game)
    {
        if (game.Players.Count < 4)
            throw new InvalidOperationException("Not enough players (minimum 4).");

        game.AssignRoles(OrderedPlayerIds);

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

/// <summary>
/// Resolves the night with a pre-collected set of player actions.
/// Used by the API after all seat turns have completed.
/// </summary>
public class ResolveNightCommand : GameCommand
{
    public ResolveNightCommand(IReadOnlyDictionary<Guid, Guid> actions)
        : base(Guid.Empty)
    {
        Actions = actions;
    }

    public IReadOnlyDictionary<Guid, Guid> Actions { get; }

    internal override void Execute(Game game)
    {
        game.ResolveNightWithActions(Actions);
    }
}