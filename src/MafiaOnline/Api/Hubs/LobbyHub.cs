using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace MafiaOnline.Api.Hubs;

public class LobbyHub : Hub
{
    private readonly ILogger<LobbyHub> _logger;

    public LobbyHub(ILogger<LobbyHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("SignalR connected: UserId={UserId}", Context.User?.FindFirstValue(ClaimTypes.NameIdentifier));
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