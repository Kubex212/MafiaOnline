using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Api.Services;

namespace Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddSingleton<ILobbyRepository, InMemoryLobbyRepository>();
            builder.Services.AddSingleton<ILobbyService, LobbyService>();
            builder.Services.AddSingleton<INightService, NightService>();
            builder.Services.AddSignalR();

            var app = builder.Build();

            app.MapHub<LobbyHub>("/lobbyHub");

            // Serve static files (so a built frontend in Api/wwwroot will be served)
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();

            app.MapControllers();

            // Fallback to index.html for SPA routes if index exists in wwwroot
            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}
