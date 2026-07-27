using MafiaOnline.Engine.Night;

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
/// Resolves the night with a pre-collected set of player actions.
/// Used by the API after all seat turns have completed.
/// </summary>
public class ResolveNightCommand : GameCommand
{
    public ResolveNightCommand(IReadOnlyList<SubmittedNightAction> actions)
        : base(Guid.Empty)
    {
        Actions = actions;
    }

    public IReadOnlyList<SubmittedNightAction> Actions { get; }

    internal override void Execute(Game game)
    {
        game.ResolveNightWithActions(Actions);
    }
}

/// <summary>
/// Resolves the day voting phase with a final set of collected votes.
/// </summary>
public class ResolveDayCommand : GameCommand
{
    public ResolveDayCommand(IReadOnlyDictionary<Guid, Guid> votes)
        : base(Guid.Empty)
    {
        Votes = votes;
    }

    public IReadOnlyDictionary<Guid, Guid> Votes { get; }

    internal override void Execute(Game game)
    {
        game.ResolveDayVoting(Votes);
    }
}