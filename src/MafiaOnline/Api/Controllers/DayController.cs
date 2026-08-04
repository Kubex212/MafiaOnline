using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Services;
using MafiaOnline.Engine.Night;
using Microsoft.AspNetCore.Mvc;

namespace MafiaOnline.Api.Controllers;

[ApiController]
[Route("api/lobbies/{code}/day")]
public class DayController : ControllerBase
{
    private readonly IDayService _dayService;
    private readonly ICurrentUser _currentUser;

    public DayController(IDayService dayService, ICurrentUser currentUser)
    {
        _dayService = dayService;
        _currentUser = currentUser;
    }

    /// <summary>Cast (or change) a vote for banishment.</summary>
    [HttpPost("vote")]
    public async Task<IActionResult> Vote(string code, [FromBody] DayVoteRequest request)
    {
        await _dayService.CastVoteAsync(code, _currentUser.UserId, request.CandidateId);
        return Ok();
    }
}
