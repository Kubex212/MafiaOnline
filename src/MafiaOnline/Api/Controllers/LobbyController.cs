using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MafiaOnline.Api.Controllers;

[ApiController]
[Route("api/lobbies")]
public class LobbiesController : ControllerBase
{
    private readonly ILobbyService _lobbyService;

    public LobbiesController(ILobbyService lobbyService)
    {
        _lobbyService = lobbyService;
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
    public async Task<ActionResult<LobbyResponse>> Start(string code, StartLobbyRequest request)
    {
        var lobby = await _lobbyService.StartLobbyAsync(code, request.PlayerId);

        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpGet("{code}/role/{playerId:guid}")]
    public async Task<ActionResult<RoleResponse>> GetRole(string code, Guid playerId)
    {
        var roleName = await _lobbyService.GetPlayerRole(code, playerId);
        var team = roleName is "Werewolf" ? "Werewolf" : "Town";

        return Ok(new RoleResponse { Name = roleName, Team = team });
    }

    [HttpPost("{code}/order/move-up")]
    public async Task<ActionResult<LobbyResponse>> MoveUp(string code, MovePlayerRequest request)
    {
        var lobby = await _lobbyService.MovePlayerUpAsync(code, request.RequestingPlayerId, request.TargetPlayerId);
        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("{code}/order/move-down")]
    public async Task<ActionResult<LobbyResponse>> MoveDown(string code, MovePlayerRequest request)
    {
        var lobby = await _lobbyService.MovePlayerDownAsync(code, request.RequestingPlayerId, request.TargetPlayerId);
        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("{code}/order/shuffle")]
    public async Task<ActionResult<LobbyResponse>> Shuffle(string code, ShufflePlayersRequest request)
    {
        var lobby = await _lobbyService.ShufflePlayersAsync(code, request.RequestingPlayerId);
        return Ok(LobbyResponse.FromLobby(lobby));
    }
}