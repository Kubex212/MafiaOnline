using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace MafiaOnline.Api.Hubs;

public class LobbyHub : Hub
{
    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"SignalR UserId: {Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)}");
        return base.OnConnectedAsync();
    }

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