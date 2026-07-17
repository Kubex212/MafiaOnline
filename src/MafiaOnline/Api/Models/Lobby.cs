using MafiaOnline.Engine;
using MafiaOnline.Engine.Commands;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;

namespace MafiaOnline.Api.Models;

public class Lobby
{
    private readonly List<LobbyPlayer> _players = new();

    public Lobby(string code)
    {
        Id = Guid.NewGuid();
        Code = code;
    }

    public Guid Id { get; }
    public string Code { get; }
    public bool Started { get; private set; }
    public Game? Game { get; private set; }
    public NightSession? NightSession { get; private set; }
    public IReadOnlyCollection<LobbyPlayer> Players => _players;

    public void AddPlayer(LobbyPlayer player)
    {
        if (Started)
            throw new InvalidOperationException("Lobby already started.");

        if (_players.Any(x => x.Nickname.Equals(player.Nickname, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Nickname already taken.");

        _players.Add(player);
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

        var game = new Game(Id);

        foreach (var lp in _players)
            game.Execute(new AddPlayerCommand(lp.Id, lp.Nickname));

        game.Execute(new StartGameCommand(requestingPlayerId));

        foreach (var lp in _players)
        {
            var ep = game.Players.First(p => p.Id == lp.Id);
            lp.SeatIndex = ep.SeatIndex;
        }

        Game = game;
        Started = true;
    }

    /// <summary>Opens a new night session for all currently alive players.</summary>
    public void BeginNightSession()
    {
        if (Game is null) throw new InvalidOperationException("Game not started.");
        var alive = _players.Where(p => Game.Players.First(ep => ep.Id == p.Id).IsAlive);
        NightSession = new NightSession(alive);
    }

    /// <summary>
    /// Returns whether the active player has a non-automatic night ability
    /// (i.e. they can choose a target rather than just skipping).
    /// </summary>
    public bool ActivePlayerHasAbility()
    {
        if (Game is null || NightSession?.ActivePlayer is null) return false;
        var ep = Game.Players.FirstOrDefault(p => p.Id == NightSession.ActivePlayer.Id);
        return ep?.Role is IHasNightAbility na && !na.NightAbility.IsAutomatic;
    }

    /// <summary>Finalises the night: sends collected actions to the engine and returns the result.</summary>
    public NightResult ResolveNight()
    {
        if (Game is null) throw new InvalidOperationException("Game not started.");
        if (NightSession is null) throw new InvalidOperationException("No active night session.");

        Game.Execute(new ResolveNightCommand(NightSession.SubmittedActions));
        NightSession = null;
        return Game.LastNightResult!;
    }
}