using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MafiaOnline.Api.Controllers;

[ApiController]
[Route("api/lobbies/{code}/day")]
public class DayController : ControllerBase
{
    private readonly IDayService _dayService;

    public DayController(IDayService dayService)
    {
        _dayService = dayService;
    }

    /// <summary>Cast (or change) a vote for banishment.</summary>
    [HttpPost("vote")]
    public async Task<IActionResult> Vote(string code, [FromBody] DayVoteRequest request)
    {
        await _dayService.CastVoteAsync(code, request.VoterId, request.CandidateId);
        return Ok();
    }
}
