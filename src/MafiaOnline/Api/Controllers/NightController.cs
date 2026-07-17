using MafiaOnline.Api.Dtos;
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

    public NightController(INightService nightService, ILobbyRepository repository)
    {
        _nightService = nightService;
        _repository = repository;
    }

    /// <summary>Host calls this to kick off the night turn loop.</summary>
    [HttpPost("begin")]
    public async Task<IActionResult> Begin(string code, [FromBody] StartLobbyRequest request)
    {
        await _nightService.BeginNightAsync(code);
        return Ok();
    }

    /// <summary>Active player submits a targeted action.</summary>
    [HttpPost("action")]
    public async Task<IActionResult> Action(string code, [FromBody] NightActionRequest request)
    {
        await _nightService.SubmitActionAsync(code, request.ActorId, request.TargetId);
        return Ok();
    }

    /// <summary>Active player skips their turn.</summary>
    [HttpPost("skip")]
    public async Task<IActionResult> Skip(string code, [FromBody] NightSkipRequest request)
    {
        await _nightService.SkipTurnAsync(code, request.ActorId);
        return Ok();
    }

    /// <summary>
    /// Private: detective fetches their clue after NightResolved fires.
    /// Returns 204 if no clue (e.g. player is not detective or config blocked it).
    /// </summary>
    [HttpGet("clue/{playerId:guid}")]
    public ActionResult<DetectiveClueResponse> GetClue(string code, Guid playerId)
    {
        var lobby = _repository.Get(code.ToUpperInvariant());
        if (lobby?.Game is null) return NotFound();

        var result = lobby.Game.LastNightResult;
        if (result is null) return NoContent();

        var clueEvent = result.Events
            .OfType<DetectiveClueEvent>()
            .FirstOrDefault(e => e.DetectiveId == playerId);

        if (clueEvent is null) return NoContent();

        var subject = lobby.Players.FirstOrDefault(p => p.Id == clueEvent.SubjectId);
        return Ok(new DetectiveClueResponse
        {
            SubjectNickname = subject?.Nickname ?? "?",
            IsWerewolf = clueEvent.IsWerewolf
        });
    }
}
