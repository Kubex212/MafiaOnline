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
}
