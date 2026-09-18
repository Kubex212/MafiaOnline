using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Dtos;

public class LobbyResponse
{
    public string Code { get; set; } = "";

    public bool Started { get; set; }

    public List<PlayerResponse> Players { get; set; } = new();

    public GameConfigDto Config { get; set; } = new();

    public string? RematchCode { get; set; }

    public bool IsPrivate { get; set; }

    public bool HasPassword { get; set; }

    public static LobbyResponse FromLobby(Lobby lobby)
    {
        return new LobbyResponse
        {
            Code = lobby.Code,
            Started = lobby.CurrentGame is not null && lobby.CurrentGame.Phase != MafiaOnline.Engine.GamePhase.Finished,
            Players = lobby.Players
                .Select((x, i) => new PlayerResponse
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    Nickname = x.Nickname,
                    IsHost = x.IsHost,
                    LobbyPosition = i,
                    SeatIndex = x.SeatIndex,
                    Ready = x.Ready
                })
                .ToList(),
            Config = GameConfigDto.FromConfig(lobby.Config),
            IsPrivate = lobby.IsPrivate,
            HasPassword = lobby.Password != null
        };
    }
}

public class PlayerResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = "";
    public bool IsHost { get; set; }

    /// <summary>Current position in the lobby order (0 = first to act at night).</summary>
    public int LobbyPosition { get; set; }

    /// <summary>-1 until the game has started; equals LobbyPosition once started.</summary>
    public int SeatIndex { get; set; } = -1;

    public bool Ready { get; set; } = false;
}