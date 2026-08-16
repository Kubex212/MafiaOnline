using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Services;
using System.Security.Claims;

namespace MafiaOnline.Api.Middleware;

public class SessionMiddleware
{
    private const string TokenHeader = "X-Session-Token";
    private readonly RequestDelegate _next;

    public SessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISessionService sessionService, CurrentUser currentUser)
    {
        var token = context.Request.Headers.TryGetValue(TokenHeader, out var rawToken)
            ? rawToken.ToString()
            : context.Request.Path.StartsWithSegments("/lobbyHub")
                ? context.Request.Query["token"].FirstOrDefault()
                : null;

        if (!string.IsNullOrWhiteSpace(token))
        {
            var userId = await sessionService.ValidateAsync(token);

            if (userId.HasValue)
            {
                currentUser.SetUserId(userId.Value);

                context.User = new ClaimsPrincipal(
                    new ClaimsIdentity(new[]
                    {
                    new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                    }, "Session"));
            }
        }

        await _next(context);
    }
}
