using MafiaOnline.Engine;
using MafiaOnline.Engine.Commands;

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

    /// <summary>Set once the game has started.</summary>
    public Game? Game { get; private set; }

    public IReadOnlyCollection<LobbyPlayer> Players => _players;

    public void AddPlayer(LobbyPlayer player)
    {
        if (Started)
            throw new InvalidOperationException("Lobby already started.");

        if (_players.Any(x => x.Nickname.Equals(player.Nickname, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Nickname already taken.");

        _players.Add(player);
    }

    /// <summary>
    /// Starts the game: creates an engine Game, adds all lobby players to it,
    /// runs StartGameCommand (which shuffles seats and assigns roles), then
    /// copies SeatIndex back onto each LobbyPlayer.
    /// </summary>
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

        // Copy seat indices back so the API can expose them
        foreach (var lp in _players)
        {
            var ep = game.Players.First(p => p.Id == lp.Id);
            lp.SeatIndex = ep.SeatIndex;
        }

        Game = game;
        Started = true;
    }
}