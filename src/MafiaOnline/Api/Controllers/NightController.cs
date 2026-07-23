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
        // adapt to engine NightAction
        var action = new NightAction(request.Targets);
        await _nightService.SubmitActionAsync(code, request.ActorId, action);
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
    /// Private: detective/seer/dog-tamer fetches their clue after NightResolved fires.
    /// Returns 204 if no clue (e.g. player has no clue or config blocked it).
    /// </summary>
    [HttpGet("clue/{playerId:guid}")]
    public async Task<ActionResult<ClueResponse>> GetClue(string code, Guid playerId)
    {
        var lobby = await _repository.Get(code.ToUpperInvariant());
        if (lobby?.Game is null) return NotFound();

        var result = lobby.Game.LastNightResult;
        if (result is null) return NoContent();

        // Detective clue
        var detectiveEvent = result.Events.OfType<DetectiveClueEvent>().FirstOrDefault(e => e.DetectiveId == playerId);
        if (detectiveEvent is not null)
        {
            return Ok(new ClueResponse
            {
                Type = "DetectiveClue",
                Data = new Dictionary<string, object>
                {
                    ["SubjectNickname"] = detectiveEvent.SubjectNickname,
                    ["IsWerewolf"] = detectiveEvent.IsWerewolf
                }
            });
        }

        // Dog tamer clue
        var dogEvent = result.Events.OfType<DogTamerClueEvent>().FirstOrDefault(e => e.DogTamerId == playerId);
        if (dogEvent is not null)
        {
            return Ok(new ClueResponse
            {
                Type = "DogTamerClue",
                Data = new Dictionary<string, object>
                {
                    ["DidBark"] = dogEvent.DidBark
                }
            });
        }

        // Seer clue
        var seerEvent = result.Events.OfType<SeerClueEvent>().FirstOrDefault(e => e.SeerId == playerId);
        if (seerEvent is not null)
        {
            // resolve nicknames for subjects if possible
            var subjAName = lobby.Players.FirstOrDefault(p => p.Id == seerEvent.SubjectA)?.Nickname ?? "?";
            var subjBName = lobby.Players.FirstOrDefault(p => p.Id == seerEvent.SubjectB)?.Nickname ?? "?";
            return Ok(new ClueResponse
            {
                Type = "SeerClue",
                Data = new Dictionary<string, object>
                {
                    ["SubjectA"] = seerEvent.SubjectA,
                    ["SubjectB"] = seerEvent.SubjectB,
                    ["SubjectAName"] = subjAName,
                    ["SubjectBName"] = subjBName,
                    ["SameTeam"] = seerEvent.SameTeam
                }
            });
        }

        return NoContent();
    }
}
