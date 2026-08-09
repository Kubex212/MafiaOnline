namespace MafiaOnline.Engine;
using global::MafiaOnline.Engine.Config;
using global::MafiaOnline.Engine.Day;
using global::MafiaOnline.Engine.Night;
using global::MafiaOnline.Engine.Roles;
using MafiaOnline.Engine.Commands;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

public class Game
{
    [JsonProperty]
    private List<Player> _players = new();
    private readonly Dictionary<(Guid Owner, ActionType type), NightAction> _pendingNightActions = new();

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

    public List<GameTurn> Turns { get; private set; } = new();

    [JsonIgnore]
    public NightResult? LastNightResult { get => Turns.LastOrDefault()?.Night; }

    [JsonIgnore]
    public DayResult? LastDayResult { get => Turns.LastOrDefault()?.Day; }

    public void Execute(GameCommand command) => command.Execute(this);

    public DateTime StartedAtUtc { get; set; }

    // ── Night ──────────────────────────────────────────────────────────────

    internal void ResolveNightWithActions(IReadOnlyList<NightAction> actions)
    {
        if (Phase != GamePhase.Night)
            throw new InvalidOperationException("Not currently the night phase.");

        var alivePlayers = _players.Where(p => p.IsAlive).ToList();
        var resolver = new NightResolver();
        var nightResult = resolver.Resolve(alivePlayers, actions, Config);
        Turns.Last().Night = nightResult;

        foreach (var killedId in LastNightResult!.Killed)
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
        var enshroudedPlayers = LastNightResult!.Events.OfType<DraculaEnshroudedEvent>().Select(e => e.TargetId).ToHashSet();
        var resolver = new DayResolver();
        var dayResult = resolver.Resolve(votes, alivePlayers, Config.Day, enshroudedPlayers);
        Turns.Last().Day = dayResult;

        foreach (var banishedId in LastDayResult!.Banished)
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
        int wolves = alive.Count(p => p.Role.Team == Team.Werewolves);
        int nonWolves   = alive.Count(p => p.Role.Team != Team.Werewolves);

        if (wolves == 0)
        {
            Winner = GameWinner.Villagers;
            Phase  = GamePhase.Finished;
            Status = GameStatus.Finished;
            return;
        }
        else if (wolves >= nonWolves)
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

    internal void AssignRoles(IReadOnlyList<Guid>? orderedPlayerIds = null)
    {
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

        var specialRolesToAssign = new List<RoleType>();

        // villager roles
        if (Config.Roles.Guardian) specialRolesToAssign.Add(RoleType.Guardian);
        if (Config.Roles.Detective) specialRolesToAssign.Add(RoleType.Detective);
        if (Config.Roles.DogTamer) specialRolesToAssign.Add(RoleType.DogTamer);
        if (Config.Roles.Poacher) specialRolesToAssign.Add(RoleType.Poacher);
        if (Config.Roles.Trailer) specialRolesToAssign.Add(RoleType.Trailer);
        if (Config.Roles.Seer) specialRolesToAssign.Add(RoleType.Seer);
        if (Config.Roles.Peeper) specialRolesToAssign.Add(RoleType.Peeper);

        // neutral roles
        if (Config.Roles.Fool) specialRolesToAssign.Add(RoleType.Fool);

        var specialRolesSet = new HashSet<RoleType>(specialRolesToAssign);
        var werewolfRolesToAssign = new List<RoleType>();

        // werewolf roles
        if (Config.Roles.Dracula) werewolfRolesToAssign.Add(RoleType.Dracula);
        if (Config.Roles.Deceiver) werewolfRolesToAssign.Add(RoleType.Deceiver);
        if (Config.Roles.Ghost) werewolfRolesToAssign.Add(RoleType.Ghost);
        if (Config.Roles.Charmer) werewolfRolesToAssign.Add(RoleType.Charmer);

        var werewolfRoleCount = werewolfRolesToAssign.Count;
        for (int i = 0; i < Config.WerewolfCount - werewolfRoleCount; i++)
            werewolfRolesToAssign.Add(RoleType.Werewolf);

        var unassignedPlayers = new HashSet<int>(Enumerable.Range(0, ordered.Count));

        var playerCount = ordered.Count;
        //var shuffledIndices = new List<int>() { 0, 1, 2, 3, 4, 5, 6, 7, 8 }.Take(playerCount).ToList();
        var shuffledIndices = Enumerable.Range(0, ordered.Count).OrderBy(_ => new Random().Next()).ToList();

        // assign werewolf roles
        for (var i = 0; i < Math.Min(werewolfRolesToAssign.Count, playerCount); i++)
        {
            var playerIndex = shuffledIndices[i];
            var werewolfRole = werewolfRolesToAssign[i];

            ordered[playerIndex].InitializeRole(werewolfRole, Config);
            unassignedPlayers.Remove(playerIndex);
        }

        // assign other roles
        var offset = Config.WerewolfCount;
        Random.Shared.Shuffle(CollectionsMarshal.AsSpan(specialRolesToAssign));
        for (var i = 0; i < Math.Min(specialRolesToAssign.Count, playerCount - offset); i++)
        {
            var playerIndex = shuffledIndices[i + offset];
            var role = specialRolesToAssign[i];

            ordered[playerIndex].InitializeRole(role, Config);
            unassignedPlayers.Remove(playerIndex);
            specialRolesSet.Remove(role);
        }

        foreach (var playerIndex in unassignedPlayers)
            ordered[playerIndex].InitializeRole(RoleType.Villager, Config);

        if(Config.Werewolf.ShowAlibi)
        {
            var unusedSpecialVillagerRoles = specialRolesSet.Where(r => r.GetTeam() == Team.Villagers).ToList();

            // if there are more werewolves than unused special villager roles, some werewolves will not get an alibi

            var werewolves = ordered.Where(player => player.Role.Team == Team.Werewolves).ToList();
            var unusedRolesCount = unusedSpecialVillagerRoles.Count;
            for (int i = 0; i < Math.Min(werewolves.Count, unusedRolesCount); i++)
            {
                var werewolf = werewolves[i];
                var randomIndex = new Random().Next(unusedSpecialVillagerRoles.Count);

                werewolf.Alibi = unusedSpecialVillagerRoles[randomIndex];
                unusedSpecialVillagerRoles.RemoveAt(randomIndex);
            }
        }
    }

    // public
    public void Initialize()
    {
        foreach (var player in _players)
            player.InitializeRole(player.RoleType, _config);
    }

    public List<Guid> GetForbiddenTargets(Guid playerId)
    {
        var player = _players.FirstOrDefault(p => p.Id == playerId);

        if (player is null)
            throw new InvalidOperationException("Player not found in game.");

        var forbiddenTargets = new List<Guid>();
        if (player.Role is IHasNightAbility nightRole && !nightRole.NightAbility.IsAutomatic)
        {
            var ability = nightRole.NightAbility;
            if (ability is not null)
            {
                var previousActions = Turns.Select(t => t.Night).Where(n => n is not null).SelectMany(n => n!.Actions).Where(a => a.Actor == playerId).ToList();
                forbiddenTargets.AddRange(ability.GetForbiddenTargets(player, _players, previousActions));
            }
        }
        return forbiddenTargets;
    }
}

public record PlayerNightInfo(string Type, Dictionary<string, object> Data);

public class GameTurn
{
    public int Index { get; set; }

    public NightResult? Night { get; set; }

    public DayResult? Day { get; set; }
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

public enum GameWinner
{
    Villagers,
    Werewolves,
    Fool,
    None,
}