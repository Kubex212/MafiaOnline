using Api.Data;
using Api.Repositories;
using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Api.Services;
using Microsoft.EntityFrameworkCore;

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

            builder.Services.AddScoped(sp =>
                new Lazy<IDayService>(() => sp.GetRequiredService<IDayService>()));

            builder.Services.AddScoped(sp =>
                new Lazy<INightService>(() => sp.GetRequiredService<INightService>()));

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
            app.UseAuthorization();

            app.MapControllers();

            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}