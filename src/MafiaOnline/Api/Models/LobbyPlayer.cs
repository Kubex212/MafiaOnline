using Newtonsoft.Json;

namespace MafiaOnline.Api.Models;

public class LobbyPlayer
{
    [JsonConstructor]
    public LobbyPlayer(Guid id, string nickname, bool isHost, int seatIndex)
    {
        Id = id;
        Nickname = nickname;
        IsHost = isHost;
        SeatIndex = seatIndex;
    }

    public LobbyPlayer(string nickname, bool isHost)
        : this(Guid.NewGuid(), nickname, isHost, -1)
    {
    }

    public Guid Id { get; }

    public string Nickname { get; }

    public bool IsHost { get; }

    public int SeatIndex { get; set; } = -1;
}