namespace MafiaOnline.Engine;
using global::MafiaOnline.Engine.Config;
using global::MafiaOnline.Engine.Night;
using global::MafiaOnline.Engine.Roles;
using MafiaOnline.Engine.Commands;

public class Game
{
    private readonly List<Player> _players = new();
    private readonly Dictionary<Guid, Guid> _pendingNightActions = new();

    public Game(Guid id, GameConfig? config = null)
    {
        Id = id;
        Config = config ?? new GameConfig();
    }

    public Guid Id { get; }
    public GameConfig Config { get; }
    public IReadOnlyCollection<Player> Players => _players;
    internal List<Player> PlayersInternal => _players;
    public GameStatus Status { get; internal set; } = GameStatus.WaitingForPlayers;
    public GamePhase Phase { get; internal set; } = GamePhase.Lobby;

    /// <summary>
    /// The result of the most recently resolved night, or null if no night has been resolved.
    /// </summary>
    public NightResult? LastNightResult { get; private set; }

    public void Execute(GameCommand command)
    {
        command.Execute(this);
    }

    /// <summary>
    /// Resolves the night with a final set of collected actions.
    /// Called by <see cref="Commands.ResolveNightCommand"/>.
    /// </summary>
    internal void ResolveNightWithActions(IReadOnlyDictionary<Guid, Guid> actions)
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");

        var alivePlayers = _players.Where(p => p.IsAlive).ToList();
        var resolver = new NightResolver();
        LastNightResult = resolver.Resolve(alivePlayers, actions);

        foreach (var killedId in LastNightResult.Killed)
            _players.FirstOrDefault(p => p.Id == killedId)?.Kill();

        Phase = GamePhase.DayDiscussion;
    }

    /// <summary>
    /// Submit a night action for a player with a non-automatic night ability.
    /// When all required actions are in, the night resolves automatically.
    /// </summary>
    internal void SubmitNightAction(Guid actorId, Guid targetId)
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");

        var actor = _players.FirstOrDefault(p => p.Id == actorId && p.IsAlive)
            ?? throw new InvalidOperationException("Player not found or not alive.");

        if (actor.Role is not IHasNightAbility nightRole || nightRole.NightAbility.IsAutomatic)
            throw new InvalidOperationException("This player has no submittable night action.");

        _pendingNightActions[actorId] = targetId;

        if (AllNightActionsSubmitted())
            ResolveNight();
    }

    /// <summary>
    /// Force-resolve the night phase (e.g. after a timeout).
    /// Players who haven't submitted actions simply skip their ability.
    /// </summary>
    internal void ForceResolveNight()
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");
        ResolveNight();
    }

    private bool AllNightActionsSubmitted()
    {
        return _players
            .Where(p => p.IsAlive && p.Role is IHasNightAbility na && !na.NightAbility.IsAutomatic)
            .All(p => _pendingNightActions.ContainsKey(p.Id));
    }

    private void ResolveNight()
    {
        var alivePlayers = _players.Where(p => p.IsAlive).ToList();
        var resolver = new NightResolver();
        LastNightResult = resolver.Resolve(alivePlayers, _pendingNightActions);

        foreach (var killedId in LastNightResult.Killed)
            _players.FirstOrDefault(p => p.Id == killedId)?.Kill();

        _pendingNightActions.Clear();
        Phase = GamePhase.DayDiscussion;
    }

    internal void AssignRoles(IReadOnlyList<Guid>? orderedPlayerIds = null)
    {
        List<Player> ordered;

        if (orderedPlayerIds is not null && orderedPlayerIds.Count == _players.Count)
        {
            // Use the explicitly supplied seat order
            ordered = orderedPlayerIds
                .Select(id => _players.First(p => p.Id == id))
                .ToList();
        }
        else
        {
            // Fall back to random shuffle
            var random = new Random();
            ordered = _players.OrderBy(_ => random.Next()).ToList();
        }

        for (int i = 0; i < ordered.Count; i++)
            ordered[i].SeatIndex = i;

        ordered[0].AssignRole(new Werewolf());
        ordered[1].AssignRole(new Guardian(Config.Guardian));
        ordered[2].AssignRole(new Detective(Config.Detective));

        foreach (var player in ordered.Skip(3))
            player.AssignRole(new Villager());
    }
}

public enum GamePhase
{
    Lobby,
    RoleAssignment,
    Night,
    DayDiscussion,
    Voting,
    Finished
}

public enum GameStatus
{
    WaitingForPlayers,
    InProgress,
    Finished
}