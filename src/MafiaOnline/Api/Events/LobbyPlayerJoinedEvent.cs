namespace MafiaOnline.Api.Events;

public class LobbyPlayerJoinedEvent
{
    public LobbyPlayerJoinedEvent(string code, string nickname)
    {
        Code = code;
        Nickname = nickname;
    }

    public string Code { get; }

    public string Nickname { get; }
}