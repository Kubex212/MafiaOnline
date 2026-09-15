namespace MafiaOnline.Api.Dtos;

public class JoinLobbyRequest
{
    public string Code { get; set; }

    public string Nickname { get; set; }

    public string? Password { get; set; }
}