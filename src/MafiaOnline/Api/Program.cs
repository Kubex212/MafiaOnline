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

            // NightService and DayService call each other; break the cycle with Lazy<T>
            builder.Services.AddSingleton<INightService, NightService>();
            builder.Services.AddSingleton<IDayService, DayService>();

            builder.Services.AddSingleton(sp =>
                new Lazy<IDayService>(() => sp.GetRequiredService<IDayService>()));

            builder.Services.AddSingleton(sp =>
                new Lazy<INightService>(() => sp.GetRequiredService<INightService>()));

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
