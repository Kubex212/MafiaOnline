using Api.Data;
using Api.Dtos;
using Api.Services;
using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;

namespace MafiaOnline.Api.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly MafiaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IStatisticsService _statisticsService;
    public MeController(MafiaDbContext db, ICurrentUser currentUser, IStatisticsService statisticsService)
    {
        _db = db;
        _currentUser = currentUser;
        _statisticsService = statisticsService;
    }

    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get()
    {
        var user = await _db.Users.FindAsync(_currentUser.UserId);

        if (user is null)
            return NotFound();

        return Ok(new MeResponse
        {
            Id = user.Id,
            PreferredNickname = user.PreferredNickname
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateMeRequest request)
    {
        var user = await _db.Users.FindAsync(_currentUser.UserId);

        if (user is null)
            return NotFound();

        user.PreferredNickname = request.PreferredNickname;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<ActionResult<UserStatisticsResponse>> GetStats()
    {
        if (!_currentUser.IsAuthenticated)
            return Unauthorized();

        var result = await _statisticsService.GetStatisticsAsync(_currentUser.UserId);

        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<GameHistoryItemResponse>>> GetHistory()
    {
        if (!_currentUser.IsAuthenticated)
            return Unauthorized();

        var result = await _statisticsService.GetRecentGamesAsync(_currentUser.UserId);

        return Ok(result);
    }
}