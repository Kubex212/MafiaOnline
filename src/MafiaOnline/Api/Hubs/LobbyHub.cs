using Microsoft.AspNetCore.SignalR;

namespace MafiaOnline.Api.Hubs;

public class LobbyHub : Hub
{
    public async Task JoinLobby(string code)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            code.ToUpperInvariant());
    }

    public async Task LeaveLobby(string code)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            code.ToUpperInvariant());
    }
}