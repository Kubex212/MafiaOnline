using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Dtos;

public class LobbyResponse
{
    public string Code { get; set; } = "";

    public bool Started { get; set; }

    public List<PlayerResponse> Players { get; set; } = [];

    public static LobbyResponse FromLobby(Lobby lobby)
    {
        return new LobbyResponse
        {
            Code = lobby.Code,
            Started = lobby.Started,
            Players = lobby.Players
                .Select(x => new PlayerResponse
                {
                    Id = x.Id,
                    Nickname = x.Nickname,
                    IsHost = x.IsHost,
                    SeatIndex = x.SeatIndex
                })
                .ToList()
        };
    }
}

public class PlayerResponse
{
    public Guid Id { get; set; }

    public string Nickname { get; set; } = "";

    public bool IsHost { get; set; }

    /// <summary>-1 until the game has started.</summary>
    public int SeatIndex { get; set; } = -1;
}