using MafiaOnline.Engine;
using MafiaOnline.Engine.Commands;
using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Day;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using Newtonsoft.Json;

namespace MafiaOnline.Api.Models;

public class Lobby
{
    [JsonProperty]
    private List<LobbyPlayer> _players = new();

    [JsonConstructor]
    public Lobby(
        Guid id,
        string code,
        Game? currentGame,
        NightSession? nightSession,
        DaySession? daySession,
        List<LobbyPlayer> players,
        GameConfig? config = null,
        string? rematchCode = null)
    {
        Id = id;
        Code = code;
        CurrentGame = currentGame;
        NightSession = nightSession;
        DaySession = daySession;
        _players = players ?? [];
        Config = config ?? new GameConfig();
        RematchCode = rematchCode;
    }

    public Lobby(string code) : this(Guid.NewGuid(), code, null, null, null, [], new GameConfig(), null)
    {
    }

    public Guid Id { get; }
    public string Code { get; }
    public bool Started => CurrentGame is not null && CurrentGame.Phase != GamePhase.Finished;
    public Game? CurrentGame { get; private set; }
    [JsonIgnore]
    public Game? Game => CurrentGame;
    public NightSession? NightSession { get; private set; }
    public DaySession? DaySession { get; private set; }
    public GameConfig Config { get; private set; }
    public IReadOnlyCollection<LobbyPlayer> Players => _players;
    public string? RematchCode { get; set; }

    // ── Lobby management ──────────────────────────────────────────────────

    public void AddPlayer(LobbyPlayer player)
    {
        if (Started)
            throw new InvalidOperationException("Lobby already started.");

        if (_players.Any(x => x.Nickname.Equals(player.Nickname, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Nickname already taken.");

        player.SeatIndex = _players.Count;
        _players.Add(player);
    }

    public void MovePlayerUp(Guid playerId)
    {
        AssertNotStarted();
        var idx = _players.FindIndex(p => p.Id == playerId);
        if (idx <= 0) return;
        (_players[idx], _players[idx - 1]) = (_players[idx - 1], _players[idx]);
        ResetLobbySeatIndices();
    }

    public void MovePlayerDown(Guid playerId)
    {
        AssertNotStarted();
        var idx = _players.FindIndex(p => p.Id == playerId);
        if (idx < 0 || idx >= _players.Count - 1) return;
        (_players[idx], _players[idx + 1]) = (_players[idx + 1], _players[idx]);
        ResetLobbySeatIndices();
    }

    public void ShufflePlayers()
    {
        AssertNotStarted();
        var random = new Random();
        for (var i = _players.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (_players[i], _players[j]) = (_players[j], _players[i]);
        }
        ResetLobbySeatIndices();
    }

    public void UpdateConfig(GameConfig config)
    {
        AssertNotStarted();
        Config = config;
    }

    public void Start(Guid requestingPlayerId)
    {
        var requesting = _players.FirstOrDefault(p => p.Id == requestingPlayerId)
            ?? throw new InvalidOperationException("Player not found in lobby.");

        if (!requesting.IsHost)
            throw new InvalidOperationException("Only the host can start the game.");

        if (_players.Count < 4)
            throw new InvalidOperationException("At least 4 players are required to start.");

        if (Started)
            throw new InvalidOperationException("Lobby already started.");

        NightSession = null;
        DaySession = null;
        RematchCode = null;

        var game = new Game(Guid.NewGuid(), Config);

        foreach (var lp in _players)
            game.Execute(new AddPlayerCommand(lp.Id, lp.Nickname));

        var seatOrder = _players.Select(p => p.Id).ToList();
        game.Execute(new StartGameCommand(requestingPlayerId, seatOrder));

        foreach (var lp in _players)
        {
            var ep = game.Players.First(p => p.Id == lp.Id);
            lp.SeatIndex = ep.SeatIndex;
        }

        CurrentGame = game;
    }

    public void ResetForNextGame()
    {
        CurrentGame = null;
        NightSession = null;
        DaySession = null;
        RematchCode = null;
        ResetLobbySeatIndices();
    }

    public void RemovePlayerByUserId(Guid userId)
    {
        AssertNotStarted();
        var idx = _players.FindIndex(p => p.UserId == userId);
        if (idx < 0) return;
        RemovePlayerAtIndex(idx);
    }

    public void RemovePlayerById(Guid playerId)
    {
        AssertNotStarted();
        var idx = _players.FindIndex(p => p.Id == playerId);
        if (idx < 0) return;
        RemovePlayerAtIndex(idx);
    }

    private void RemovePlayerAtIndex(int idx)
    {
        _players.RemoveAt(idx);
        ResetLobbySeatIndices();

        if (!_players.Any(p => p.IsHost) && _players.Count > 0)
            _players[0].IsHost = true;
    }

    // ── Night ──────────────────────────────────────────────────────────────

    public void BeginNightSession()
    {
        if (CurrentGame is null) throw new InvalidOperationException("Game not started.");

        CurrentGame.Execute(new StartNightCommand(Guid.Empty));

        var alive = _players.Where(p => CurrentGame.Players.First(ep => ep.Id == p.Id).IsAlive);
        NightSession = new NightSession(alive);
        DaySession = null;
    }

    public bool ActivePlayerHasAbility()
    {
        if (CurrentGame is null || NightSession?.ActivePlayer is null) return false;

        var enginePlayer = CurrentGame.Players.FirstOrDefault(p => p.Id == NightSession.ActivePlayer.Id);
        if (enginePlayer?.Role is not IHasNightAbility na)
            return false;
        if (na.NightAbility.IsAutomatic)
            return false;
        if (na.NightAbility.Cooldown.HasValue)
        {
            var lastUsedNight = CurrentGame.Turns
                .Where(t => t.Night != null && t.Night.Actions.Any(a => a.Actor == enginePlayer.Id && a.ActionType == ActionType.Ability))
                .OrderByDescending(t => t.Index)
                .FirstOrDefault();
            var isOnCooldown = lastUsedNight != null && (CurrentGame.Turns.Count - lastUsedNight.Index) < na.NightAbility.Cooldown.Value;
            return !isOnCooldown;
        }

        return true;
    }

    public NightResult ResolveNight()
    {
        if (CurrentGame is null) throw new InvalidOperationException("Game not started.");
        if (NightSession is null) throw new InvalidOperationException("No active night session.");

        CurrentGame.Execute(new ResolveNightCommand(NightSession.SubmittedActions));
        NightSession = null;
        return CurrentGame.LastNightResult!;
    }

    // ── Day ───────────────────────────────────────────────────────────────

    public void BeginDaySession()
    {
        if (CurrentGame is null) throw new InvalidOperationException("Game not started.");

        var alive = _players.Where(p => CurrentGame.Players.First(ep => ep.Id == p.Id).IsAlive);
        DaySession = new DaySession(alive);
        NightSession = null;
    }

    /// <summary>
    /// Cast a vote. Returns the updated vote map so the service can broadcast it.
    /// When <see cref="DaySession.AllVoted"/> is true the caller should resolve immediately.
    /// </summary>
    public IReadOnlyDictionary<Guid, Guid?> CastVote(Guid voterId, Guid? candidateId)
    {
        if (DaySession is null) throw new InvalidOperationException("No active day session.");
        DaySession.CastVote(voterId, candidateId);
        return DaySession.Votes;
    }

    public DayResult ResolveDay()
    {
        if (CurrentGame is null) throw new InvalidOperationException("Game not started.");
        if (DaySession is null) throw new InvalidOperationException("No active day session.");

        // Convert nullable votes map to non-null map expected by engine (exclude skips)
        var votesForEngine = DaySession.Votes
            .Where(kvp => kvp.Value.HasValue)
            .ToDictionary(k => k.Key, v => v.Value!.Value);

        CurrentGame.Execute(new ResolveDayCommand(votesForEngine));
        DaySession = null;
        return CurrentGame.LastDayResult!;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void AssertNotStarted()
    {
        if (Started) throw new InvalidOperationException("Cannot modify lobby after the game has started.");
    }

    private void ResetLobbySeatIndices()
    {
        for (var i = 0; i < _players.Count; i++)
            _players[i].SeatIndex = i;
    }
}