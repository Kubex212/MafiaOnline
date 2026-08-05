using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Api.Services;
using MafiaOnline.Engine.Night;
using Microsoft.AspNetCore.Mvc;

namespace MafiaOnline.Api.Controllers;

[ApiController]
[Route("api/lobbies/{code}/night")]
public class NightController : ControllerBase
{
    private readonly INightService _nightService;
    private readonly ILobbyRepository _repository;
    private readonly ICurrentUser _currentUser;

    public NightController(INightService nightService, ILobbyRepository repository, ICurrentUser currentUser)
    {
        _nightService = nightService;
        _repository = repository;
        _currentUser = currentUser;
    }

    /// <summary>Host calls this to kick off the night turn loop.</summary>
    [HttpPost("begin")]
    public async Task<IActionResult> Begin(string code)
    {
        await _nightService.BeginNightAsync(code);
        return Ok();
    }

    /// <summary>Active player submits a targeted action.</summary>
    [HttpPost("action")]
    public async Task<IActionResult> Action(string code, [FromBody] NightActionRequest request)
    {
        await _nightService.SubmitActionAsync(code, _currentUser.UserId, request.Targets, request.ActionType);
        return Ok();
    }

    /// <summary>Active player skips their turn.</summary>
    [HttpPost("skip")]
    public async Task<IActionResult> Skip(string code)
    {
        await _nightService.SkipTurnAsync(code, _currentUser.UserId);
        return Ok();
    }

    /// <summary>
    /// Private: detective/seer/dog-tamer fetches their clue after NightResolved fires.
    /// Returns 204 if no clue (e.g. player has no clue or config blocked it).
    /// </summary>
    [HttpGet("clue")]
    public async Task<ActionResult<ClueResponse>> GetClue(string code)
    {
        var lobby = await _repository.Get(code.ToUpperInvariant());
        if (lobby?.Game is null) return NotFound();

        var result = lobby.Game.LastNightResult;
        if (result is null) return NoContent();

        var playerId = lobby.Players.FirstOrDefault(p => p.UserId == _currentUser.UserId)?.Id;
        if (playerId is null) return NotFound();

        var clueEvent = result.Events.OfType<ClueEvent>().SingleOrDefault(e => e.ClueOwnerId == playerId);
        if (clueEvent is null) return NoContent();

        return Ok(new ClueResponse
        {
            Type = clueEvent.ClueType.ToString(),
            Clue = clueEvent.DisplayedClue,
            Data = clueEvent.GetAdditionalInfoDictionary(lobby.Game.Players)
        });
    }
}
