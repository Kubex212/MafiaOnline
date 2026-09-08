using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Api.Dtos;

namespace MafiaOnline.Api.Controllers;

[ApiController]
[Route("api/lobbies")]
public class LobbiesController : ControllerBase
{
    private readonly ILobbyService _lobbyService;
    private readonly ICurrentUser _currentUser;

    public LobbiesController(ILobbyService lobbyService, ICurrentUser currentUser)
    {
        _lobbyService = lobbyService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<LobbyResponse>> Create(CreateLobbyRequest request)
    {
        var lobby = await _lobbyService.CreateLobby(request.Nickname);

        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("join")]
    public async Task<ActionResult<LobbyResponse>> Join(JoinLobbyRequest request)
    {
        var lobby = await _lobbyService.JoinLobbyAsync(
            request.Code,
            request.Nickname);

        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<LobbyResponse>> Get(string code)
    {
        var lobby = await _lobbyService.GetLobby(code);

        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpGet("{code}/history")]
    public async Task<ActionResult<IReadOnlyList<LobbyGameHistoryItemResponse>>> GetHistory(string code, [FromQuery] int take = 20)
    {
        var history = await _lobbyService.GetLobbyHistoryAsync(code, take);
        return Ok(history);
    }

    [HttpPost("{code}/start")]
    public async Task<ActionResult<LobbyResponse>> Start(string code)
    {
        var lobby = await _lobbyService.StartLobbyAsync(code);

         return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpGet("{code}/role")]
    public async Task<ActionResult<RoleResponse>> GetRole(string code)
    {
        var role = await _lobbyService.GetPlayerRole(code);

        return Ok(role);
    }

    [HttpPost("{code}/order/move-up")]
    public async Task<ActionResult<LobbyResponse>> MoveUp(string code, MovePlayerRequest request)
    {
        var lobby = await _lobbyService.MovePlayerUpAsync(code, request.TargetPlayerId);
        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("{code}/order/move-down")]
    public async Task<ActionResult<LobbyResponse>> MoveDown(string code, MovePlayerRequest request)
    {
        var lobby = await _lobbyService.MovePlayerDownAsync(code, request.TargetPlayerId);
        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("{code}/order/shuffle")]
    public async Task<ActionResult<LobbyResponse>> Shuffle(string code)
    {
        var lobby = await _lobbyService.ShufflePlayersAsync(code);
        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("{code}/reconnect")]
    public async Task<ActionResult<ReconnectResponse>> Reconnect(string code)
    {
        var response = await _lobbyService.ReconnectPlayerAsync(code);
        return Ok(response);
    }

    [HttpPost("{code}/config")]
    public async Task<ActionResult<LobbyResponse>> UpdateConfig(string code, UpdateConfigRequest request)
    {
        var lobby = await _lobbyService.UpdateConfigAsync(code, request.Config);
        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("{code}/rematch")]
    public async Task<ActionResult<LobbyResponse>> Rematch(string code)
    {
        var newLobby = await _lobbyService.CreateRematchAsync(code);
        return Ok(LobbyResponse.FromLobby(newLobby));
    }

    [HttpPost("{code}/leave")]
    public async Task<ActionResult<LobbyResponse?>> Leave(string code)
    {
        var lobby = await _lobbyService.LeaveLobbyAsync(code);
        if (lobby == null) return NoContent();
        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("{code}/kick")]
    public async Task<ActionResult<LobbyResponse?>> Kick(string code, MovePlayerRequest request)
    {
        var lobby = await _lobbyService.KickPlayerAsync(code, request.TargetPlayerId);
        if (lobby == null) return NoContent();
        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        await _lobbyService.DeleteLobbyAsync(code);
        return NoContent();
    }
}