using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Services;
using Microsoft.AspNetCore.Mvc;

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
}