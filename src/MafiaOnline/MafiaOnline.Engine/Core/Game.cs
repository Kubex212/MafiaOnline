namespace MafiaOnline.Engine;
using global::MafiaOnline.Engine.Config;
using global::MafiaOnline.Engine.Day;
using global::MafiaOnline.Engine.Night;
using global::MafiaOnline.Engine.Roles;
using MafiaOnline.Engine.Commands;
using Newtonsoft.Json;

public class Game
{
    [JsonProperty]
    private List<Player> _players = new();
    private readonly Dictionary<Guid, NightAction> _pendingNightActions = new();

    [JsonConstructor]
    public Game(Guid id, GameConfig? config = null)
    {
        Id = id;
        _config = config ?? new GameConfig();
    }

    public Guid Id { get; }

    [JsonProperty]
    private GameConfig _config = new GameConfig();
    public GameConfig Config { get => _config; internal set => _config = value; }

    public IReadOnlyCollection<Player> Players => _players;
    internal List<Player> PlayersInternal => _players;

    [JsonProperty]
    private GameStatus _status = GameStatus.WaitingForPlayers;
    public GameStatus Status { get => _status; internal set => _status = value; }

    [JsonProperty]
    private GamePhase _phase = GamePhase.Lobby;
    public GamePhase Phase { get => _phase; internal set => _phase = value; }

    [JsonProperty]
    private GameWinner _winner = GameWinner.None;
    public GameWinner Winner { get => _winner; internal set => _winner = value; }

    [JsonProperty]
    private NightResult? _lastNightResult;
    public NightResult? LastNightResult { get => _lastNightResult; internal set => _lastNightResult = value; }

    [JsonProperty]
    private DayResult? _lastDayResult;
    public DayResult? LastDayResult { get => _lastDayResult; internal set => _lastDayResult = value; }

    public void Execute(GameCommand command) => command.Execute(this);

    // ── Night ──────────────────────────────────────────────────────────────

    internal void ResolveNightWithActions(IReadOnlyDictionary<Guid, Guid> actions)
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");

        var alivePlayers = _players.Where(p => p.IsAlive).ToList();
        var resolver = new NightResolver();
        // adapt legacy actions
        var adapted = actions.ToDictionary(kvp => kvp.Key, kvp => new NightAction(new[] { kvp.Value } as IEnumerable<Guid>));
        LastNightResult = resolver.Resolve(alivePlayers, adapted);

        foreach (var killedId in LastNightResult.Killed)
            _players.FirstOrDefault(p => p.Id == killedId)?.Kill();

        CheckWinCondition();

        if (Phase != GamePhase.Finished)
            Phase = GamePhase.DayDiscussion;
    }

    // New overload accepting NightAction payloads
    internal void ResolveNightWithActions(IReadOnlyDictionary<Guid, NightAction> actions)
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");

        var alivePlayers = _players.Where(p => p.IsAlive).ToList();
        var resolver = new NightResolver();
        LastNightResult = resolver.Resolve(alivePlayers, actions);

        foreach (var killedId in LastNightResult.Killed)
            _players.FirstOrDefault(p => p.Id == killedId)?.Kill();

        CheckWinCondition();

        if (Phase != GamePhase.Finished)
            Phase = GamePhase.DayDiscussion;
    }

    // ── Day ────────────────────────────────────────────────────────────────

    internal void ResolveDayVoting(IReadOnlyDictionary<Guid, Guid> votes)
    {
        if (Phase != GamePhase.DayDiscussion)
            throw new InvalidOperationException("Not currently the day phase.");

        var alivePlayers = _players.Where(p => p.IsAlive).Select(p => p.Id).ToList();
        var resolver = new DayResolver();
        LastDayResult = resolver.Resolve(votes, alivePlayers, Config.Day);

        foreach (var banishedId in LastDayResult.Banished)
            _players.FirstOrDefault(p => p.Id == banishedId)?.Kill();

        CheckWinCondition();

        if (Phase != GamePhase.Finished)
            Phase = GamePhase.Night;
    }

    // ── Win condition ──────────────────────────────────────────────────────

    /// <summary>
    /// Villagers win when there are no werewolves left.
    /// Werewolves win when they are at least as numerous as the remaining town players.
    /// Roles implementing IWinCondition can also trigger role-specific victories.
    /// </summary>
    private void CheckWinCondition()
    {
        var alive = _players.Where(p => p.IsAlive).ToList();
        int wolves = alive.Count(p => p.Role.TeamId == 1);
        int town   = alive.Count(p => p.Role.TeamId == 0);

        if (wolves == 0)
        {
            Winner = GameWinner.Villagers;
            Phase  = GamePhase.Finished;
            Status = GameStatus.Finished;
            return;
        }
        else if (wolves >= town)
        {
            Winner = GameWinner.Werewolves;
            Phase  = GamePhase.Finished;
            Status = GameStatus.Finished;
            return;
        }

        // Evaluate role-specific win conditions (e.g. Fool)
        // Include players who were just banished (in LastDayResult) because some win conditions
        // (like Fool) trigger when they are banished rather than when alive.
        var candidates = _players.Where(p => p.IsAlive || (LastDayResult?.Banished.Contains(p.Id) ?? false)).ToList();
        foreach (var p in candidates)
        {
            if (p.Role is IWinCondition wc && wc.Evaluate(this, p))
            {
                // Fool or other role-specific winner: record a neutral/role winner
                Winner = GameWinner.Fool;
                Phase = GamePhase.Finished;
                Status = GameStatus.Finished;
                return;
            }
        }
    }

    // ── Existing helpers (kept for completeness) ───────────────────────────

    internal void SubmitNightAction(Guid actorId, Guid targetId)
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");

        var actor = _players.FirstOrDefault(p => p.Id == actorId && p.IsAlive)
            ?? throw new InvalidOperationException("Player not found or not alive.");

        if (actor.Role is not IHasNightAbility nightRole || nightRole.NightAbility.IsAutomatic)
            throw new InvalidOperationException("This player has no submittable night action.");

        _pendingNightActions[actorId] = new NightAction(new[] { targetId } as IEnumerable<Guid>);

        if (AllNightActionsSubmitted()) ResolveNightLegacy();
    }

    // New submission allowing multiple targets
    internal void SubmitNightAction(Guid actorId, NightAction action)
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");

        var actor = _players.FirstOrDefault(p => p.Id == actorId && p.IsAlive)
            ?? throw new InvalidOperationException("Player not found or not alive.");

        if (actor.Role is not IHasNightAbility nightRole || nightRole.NightAbility.IsAutomatic)
            throw new InvalidOperationException("This player has no submittable night action.");

        _pendingNightActions[actorId] = action;

        if (AllNightActionsSubmitted()) ResolveNightLegacy();
    }

    internal void ForceResolveNight()
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");
        ResolveNightLegacy();
    }

    private bool AllNightActionsSubmitted() =>
        _players
            .Where(p => p.IsAlive && p.Role is IHasNightAbility na && !na.NightAbility.IsAutomatic)
            .All(p => _pendingNightActions.ContainsKey(p.Id));

    private void ResolveNightLegacy()
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
        // TODO: replace those hardcoded role counts with values based on the configuration
        // TODO: also make it so adding roles is easier and doesnt require changing this method
        var werewolfCount = 1;
        var guardianCount = 1;
        var detectiveCount = 1;

        List<Player> ordered;

        if (orderedPlayerIds is not null && orderedPlayerIds.Count == _players.Count)
            ordered = orderedPlayerIds.Select(id => _players.First(p => p.Id == id)).ToList();
        else
        {
            var random = new Random();
            ordered = _players.OrderBy(_ => random.Next()).ToList();
        }

        for (int i = 0; i < ordered.Count; i++)
            ordered[i].SeatIndex = i;

        // TODO: replace this hardcoded role assignment with a dynamic assignment based on the counts in configuration
        var specialRolesToAssign = new List<IRole>()
        {
            new Werewolf(),
            new DogTamer(Config.DogTamer),
            new Seer(),
            new Fool()
        };

        var unassignedPlayers = new HashSet<int>(Enumerable.Range(0, ordered.Count));

        var shuffledIndices = Enumerable.Range(0, ordered.Count).OrderBy(_ => new Random().Next()).ToList();
        for(var i = 0; i < specialRolesToAssign.Count; i++)
        {
            var playerIndex = shuffledIndices[i];
            var role = specialRolesToAssign[i];
            ordered[playerIndex].AssignRole(role);
            unassignedPlayers.Remove(playerIndex);
        }

        foreach (var playerIndex in unassignedPlayers)
            ordered[playerIndex].AssignRole(new Villager());
    }
}

public record PlayerNightInfo(string Type, Dictionary<string, object> Data);

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

public enum GameWinner
{
    None,
    Villagers,
    Werewolves,
    Fool
}