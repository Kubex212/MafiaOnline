using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class MafiaDbContext : DbContext
{
    public MafiaDbContext(DbContextOptions<MafiaDbContext> options)
        : base(options)
    {
    }

    public DbSet<LobbyEntity> Lobbies { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<SessionEntity> Sessions { get; set; }
    public DbSet<ExternalLoginEntity> ExternalLogins { get; set; }
    public DbSet<GameHistoryEntity> GameHistory { get; set; }
    public DbSet<LobbyGameEntity> LobbyGames { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SessionEntity>(b =>
        {
            b.HasIndex(s => s.TokenHash).IsUnique();
            b.HasOne(s => s.User)
             .WithMany()
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExternalLoginEntity>(b =>
        {
            b.HasIndex(e => new { e.Provider, e.ProviderUserId }).IsUnique();
            b.HasOne(e => e.User)
             .WithMany()
             .HasForeignKey(e => e.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameHistoryEntity>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.PlayedAtUtc });
            e.HasIndex(x => x.LobbyId);
            e.HasIndex(x => new { x.LobbyId, x.PlayedAtUtc });

            e.Property(x => x.Role)
                .HasConversion<int>();
        });

        modelBuilder.Entity<LobbyGameEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LobbyId);
            e.HasIndex(x => new { x.LobbyId, x.FinishedAtUtc });
            e.HasIndex(x => x.LobbyCode);
        });
    }
}
