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
        bool started,
        Game? game,
        NightSession? nightSession,
        DaySession? daySession,
        List<LobbyPlayer> players,
        GameConfig? config = null)
    {
        Id = id;
        Code = code;
        Started = started;
        Game = game;
        NightSession = nightSession;
        DaySession = daySession;
        _players = players ?? new List<LobbyPlayer>();
        Config = config ?? new GameConfig();
    }

    public Lobby(string code)
        : this(
            Guid.NewGuid(),
            code,
            false,
            null,
            null,
            null,
            new List<LobbyPlayer>(),
            new GameConfig())
    {
    }

    public Guid Id { get; }
    public string Code { get; }
    public bool Started { get; private set; }
    public Game? Game { get; private set; }
    public NightSession? NightSession { get; private set; }
    public DaySession? DaySession { get; private set; }
    public GameConfig Config { get; private set; }
    public IReadOnlyCollection<LobbyPlayer> Players => _players;

    // ── Lobby management ──────────────────────────────────────────────────

    public void AddPlayer(LobbyPlayer player)
    {
        if (Started)
            throw new InvalidOperationException("Lobby already started.");

        if (_players.Any(x => x.Nickname.Equals(player.Nickname, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Nickname already taken.");

        _players.Add(player);
    }

    public void MovePlayerUp(Guid playerId)
    {
        AssertNotStarted();
        var idx = _players.FindIndex(p => p.Id == playerId);
        if (idx <= 0) return;
        (_players[idx], _players[idx - 1]) = (_players[idx - 1], _players[idx]);
    }

    public void MovePlayerDown(Guid playerId)
    {
        AssertNotStarted();
        var idx = _players.FindIndex(p => p.Id == playerId);
        if (idx < 0 || idx >= _players.Count - 1) return;
        (_players[idx], _players[idx + 1]) = (_players[idx + 1], _players[idx]);
    }

    public void ShufflePlayers()
    {
        AssertNotStarted();
        var random = new Random();
        for (int i = _players.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (_players[i], _players[j]) = (_players[j], _players[i]);
        }
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

        var game = new Game(Id, Config);

        foreach (var lp in _players)
            game.Execute(new AddPlayerCommand(lp.Id, lp.Nickname));

        var seatOrder = _players.Select(p => p.Id).ToList();
        game.Execute(new StartGameCommand(requestingPlayerId, seatOrder));

        foreach (var lp in _players)
        {
            var ep = game.Players.First(p => p.Id == lp.Id);
            lp.SeatIndex = ep.SeatIndex;
        }

        Game = game;
        Started = true;
    }

    // ── Night ──────────────────────────────────────────────────────────────

    public void BeginNightSession()
    {
        if (Game is null) throw new InvalidOperationException("Game not started.");

        var alive = _players.Where(p => Game.Players.First(ep => ep.Id == p.Id).IsAlive);
        NightSession = new NightSession(alive);
    }

    public bool ActivePlayerHasAbility()
    {
        if (Game is null || NightSession?.ActivePlayer is null) return false;
        var ep = Game.Players.FirstOrDefault(p => p.Id == NightSession.ActivePlayer.Id);
        return ep?.Role is IHasNightAbility na && !na.NightAbility.IsAutomatic;
    }

    public NightResult ResolveNight()
    {
        if (Game is null) throw new InvalidOperationException("Game not started.");
        if (NightSession is null) throw new InvalidOperationException("No active night session.");

        Game.Execute(new ResolveNightCommand(NightSession.SubmittedActions));
        NightSession = null;
        return Game.LastNightResult!;
    }

    // ── Day ───────────────────────────────────────────────────────────────

    public void BeginDaySession()
    {
        if (Game is null) throw new InvalidOperationException("Game not started.");

        var alive = _players.Where(p => Game.Players.First(ep => ep.Id == p.Id).IsAlive);
        DaySession = new DaySession(alive);
    }

    /// <summary>
    /// Cast a vote. Returns the updated vote map so the service can broadcast it.
    /// When <see cref="DaySession.AllVoted"/> is true the caller should resolve immediately.
    /// </summary>
    public IReadOnlyDictionary<Guid, Guid> CastVote(Guid voterId, Guid candidateId)
    {
        if (DaySession is null) throw new InvalidOperationException("No active day session.");
        DaySession.CastVote(voterId, candidateId);
        return DaySession.Votes;
    }

    public DayResult ResolveDay()
    {
        if (Game is null) throw new InvalidOperationException("Game not started.");
        if (DaySession is null) throw new InvalidOperationException("No active day session.");

        Game.Execute(new ResolveDayCommand(DaySession.Votes));
        DaySession = null;
        return Game.LastDayResult!;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void AssertNotStarted()
    {
        if (Started) throw new InvalidOperationException("Cannot modify lobby after the game has started.");
    }
}