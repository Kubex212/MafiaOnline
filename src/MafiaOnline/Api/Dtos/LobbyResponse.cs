using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Dtos;

public class LobbyResponse
{
    public string Code { get; set; } = "";

    public bool Started { get; set; }

    public List<PlayerResponse> Players { get; set; } = [];

    public GameConfigDto Config { get; set; } = new();

    public static LobbyResponse FromLobby(Lobby lobby)
    {
        return new LobbyResponse
        {
            Code = lobby.Code,
            Started = lobby.Started,
            // Players are already in lobby/seat order — preserve that order
            Players = lobby.Players
                .Select((x, i) => new PlayerResponse
                {
                    Id = x.Id,
                    Nickname = x.Nickname,
                    IsHost = x.IsHost,
                    LobbyPosition = i,
                    SeatIndex = x.SeatIndex
                })
                .ToList(),
            Config = GameConfigDto.FromConfig(lobby.Config)
        };
    }
}

public class PlayerResponse
{
    public Guid Id { get; set; }

    public string Nickname { get; set; } = "";

    public bool IsHost { get; set; }

    /// <summary>Current position in the lobby order (0 = first to act at night).</summary>
    public int LobbyPosition { get; set; }

    /// <summary>-1 until the game has started; equals LobbyPosition once started.</summary>
    public int SeatIndex { get; set; } = -1;
}