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
    public ActionResult<LobbyResponse> Create(CreateLobbyRequest request)
    {
        var lobby = _lobbyService.CreateLobby(request.Nickname);

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
    public ActionResult<LobbyResponse> Get(string code)
    {
        var lobby = _lobbyService.GetLobby(code);

        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpPost("{code}/start")]
    public async Task<ActionResult<LobbyResponse>> Start(string code, StartLobbyRequest request)
    {
        var lobby = await _lobbyService.StartLobbyAsync(code, request.PlayerId);

        return Ok(LobbyResponse.FromLobby(lobby));
    }

    [HttpGet("{code}/role/{playerId:guid}")]
    public ActionResult<RoleResponse> GetRole(string code, Guid playerId)
    {
        var roleName = _lobbyService.GetPlayerRole(code, playerId);
        var team = roleName is "Werewolf" ? "Werewolf" : "Town";

        return Ok(new RoleResponse { Name = roleName, Team = team });
    }
}