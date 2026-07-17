namespace MafiaOnline.Api.Models;

public class LobbyPlayer
{
    public LobbyPlayer(string nickname, bool isHost)
    {
        Id = Guid.NewGuid();
        Nickname = nickname;
        IsHost = isHost;
    }

    public Guid Id { get; }

    public string Nickname { get; }

    public bool IsHost { get; }

    /// <summary>Set once the game has started; mirrors the engine Player's SeatIndex.</summary>
    public int SeatIndex { get; set; } = -1;
}