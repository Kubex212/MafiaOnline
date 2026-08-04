using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Services;

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
       if (context.Request.Headers.TryGetValue(TokenHeader, out var rawToken) &&
            !string.IsNullOrWhiteSpace(rawToken))
        {
            var userId = await sessionService.ValidateAsync(rawToken!);
            if (userId.HasValue)
                currentUser.SetUserId(userId.Value);
        }

        await _next(context);
    }
}
