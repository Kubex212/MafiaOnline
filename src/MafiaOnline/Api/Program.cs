using Api.Data;
using Api.Repositories;
using Api.Services;
using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Middleware;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddDbContext<MafiaDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("MafiaDb")));

            builder.Services.AddScoped<ILobbyRepository, PostgresLobbyRepository>();
            builder.Services.AddScoped<ILobbyService, LobbyService>();
            builder.Services.AddScoped<INightService, NightService>();
            builder.Services.AddScoped<IDayService, DayService>();
            builder.Services.AddScoped<IGameHistoryService, GameHistoryService>();
            builder.Services.AddScoped<IStatisticsService, StatisticsService>();

            builder.Services.AddScoped(sp =>
                new Lazy<IDayService>(() => sp.GetRequiredService<IDayService>()));
            builder.Services.AddScoped(sp =>
                new Lazy<INightService>(() => sp.GetRequiredService<INightService>()));

            // Identity & sessions
            builder.Services.AddScoped<ISessionService, SessionService>();
            builder.Services.AddScoped<IExternalAuthService, ExternalAuthService>();
            builder.Services.AddScoped<CurrentUser>();
            builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());

            // Auth: cookie (needed to store OAuth state) + Discord OAuth
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            .AddOAuth("Discord", options =>
            {
                options.ClientId = builder.Configuration["Discord:ClientId"]!;
                options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;

                options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
                options.TokenEndpoint = "https://discord.com/api/oauth2/token";
                options.UserInformationEndpoint = "https://discord.com/api/users/@me";

                options.Scope.Add("identify");

                options.CallbackPath = "/api/auth/discord/callback";

                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async ctx =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
                        var response = await ctx.Backchannel.SendAsync(request);
                        response.EnsureSuccessStatusCode();

                        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        var root = doc.RootElement;

                        if (root.TryGetProperty("id", out var id))
                            ctx.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier, id.GetString()!));

                        if (root.TryGetProperty("username", out var username))
                            ctx.Identity!.AddClaim(new Claim(ClaimTypes.Name, username.GetString()!));
                    },
                    OnTicketReceived = async ctx =>
                    {
                        var providerUserId = ctx.Principal!
                            .FindFirstValue(ClaimTypes.NameIdentifier)!;

                        var sessionService = ctx.HttpContext.RequestServices
                             .GetRequiredService<ISessionService>();
                        var externalAuth = ctx.HttpContext.RequestServices
                             .GetRequiredService<IExternalAuthService>();

                        var userId = await externalAuth.FindOrCreateUserAsync("Discord", providerUserId);
                        var session = await sessionService.CreateForUserAsync(userId);

                        // create/find your user
                        // create session

                        ctx.Response.Redirect(
                            $"/?token={Uri.EscapeDataString(session.Token)}"
                        );

                        ctx.HandleResponse();
                    }
                };
            })
            .AddOAuth("Google", options =>
            {
                options.ClientId = builder.Configuration["Google:ClientId"]!;
                options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;

                options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
                options.TokenEndpoint = "https://oauth2.googleapis.com/token";
                options.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";

                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                options.CallbackPath = "/api/auth/google/callback";

                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async ctx =>
                    {
                        var request = new HttpRequestMessage(
                            HttpMethod.Get,
                            ctx.Options.UserInformationEndpoint);

                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);

                        var response = await ctx.Backchannel.SendAsync(request);
                        response.EnsureSuccessStatusCode();

                        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                        var root = doc.RootElement;

                        if (root.TryGetProperty("sub", out var id))
                            ctx.Identity!.AddClaim( new Claim(ClaimTypes.NameIdentifier, id.GetString()!));

                        if (root.TryGetProperty("name", out var name))
                            ctx.Identity!.AddClaim(new Claim(ClaimTypes.Name, name.GetString()!));
                    },

                    OnTicketReceived = async ctx =>
                    {
                        var providerUserId = ctx.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
                        var sessionService = ctx.HttpContext.RequestServices.GetRequiredService<ISessionService>();
                        var externalAuth = ctx.HttpContext.RequestServices.GetRequiredService<IExternalAuthService>();

                        var userId = await externalAuth.FindOrCreateUserAsync("Google", providerUserId);
                        var session = await sessionService.CreateForUserAsync(userId);

                        ctx.Response.Redirect($"/?token={Uri.EscapeDataString(session.Token)}");

                        ctx.HandleResponse();
                    }
                };
            });

            builder.Services.AddSignalR();

            var app = builder.Build();


            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<MafiaDbContext>();
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                }
            }


            app.MapHub<LobbyHub>("/lobbyHub");
            app.UseDefaultFiles();
            app.UseStaticFiles();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<SessionMiddleware>();

            app.MapControllers();
            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}