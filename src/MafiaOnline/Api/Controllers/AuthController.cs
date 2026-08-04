using System.Security.Claims;
using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace MafiaOnline.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string TokenHeader = "X-Session-Token";
    private const string DiscordScheme = "Discord";
    private readonly ISessionService _sessionService;
    private readonly ICurrentUser _currentUser;
    private readonly IExternalAuthService _externalAuth;

    public AuthController(ISessionService sessionService, ICurrentUser currentUser, IExternalAuthService externalAuth)
    {
        _sessionService = sessionService;
        _currentUser = currentUser;
        _externalAuth = externalAuth;
    }

    /// <summary>
    /// Returns current session info. Creates a new anonymous user + session if no valid token is provided.
    /// </summary>
    [HttpGet("session")]
    public async Task<ActionResult<SessionResponse>> GetOrCreate()
    {
        if (_currentUser.IsAuthenticated)
            return Ok(new SessionResponse(_currentUser.UserId, Token: null));

        var result = await _sessionService.CreateAsync();
        return Ok(new SessionResponse(result.UserId, result.Token));
    }

    /// <summary>
    /// Redirects the browser to Discord's OAuth2 authorization page.
    /// </summary>
    [HttpGet("discord")]
    public IActionResult DiscordLogin()
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(DiscordCallback), "Auth")
        };
        return Challenge(props, DiscordScheme);
    }

    /// <summary>
    /// Discord redirects back here after the user approves.
    /// Creates/finds the user, issues a session token, and redirects to the frontend with the token in the URL.
    /// </summary>
    [HttpGet("discord/callback")]
    public async Task<IActionResult> DiscordCallback()
    {
        var result = await HttpContext.AuthenticateAsync(DiscordScheme);
        if (!result.Succeeded)
            return BadRequest("Discord authentication failed.");

        var providerUserId = result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No NameIdentifier claim from Discord.");

        var userId = await _externalAuth.FindOrCreateUserAsync("Discord", providerUserId);
        var session = await _sessionService.CreateForUserAsync(userId);

        // Redirect to frontend — the JS will pick up the token from the URL query string
        return Redirect($"/?token={Uri.EscapeDataString(session.Token)}");
    }
}

public record SessionResponse(Guid UserId, string? Token);
