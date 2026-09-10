using Newtonsoft.Json;

namespace MafiaOnline.Api.Models;

public class LobbyPlayer
{
    [JsonConstructor]
    public LobbyPlayer(Guid id, Guid userId, string nickname, bool isHost, int seatIndex, bool readyForRematch)
    {
        Id = id;
        UserId = userId;
        Nickname = nickname;
        IsHost = isHost;
        SeatIndex = seatIndex;
        Ready = readyForRematch;
    }

    public LobbyPlayer(Guid userId, string nickname, bool isHost)
        : this(Guid.NewGuid(), userId, nickname, isHost, -1, false)
    {
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string Nickname { get; }

    public bool IsHost { get; set; }

    public int SeatIndex { get; set; } = -1;

    public bool Ready { get; set; } = false;
}