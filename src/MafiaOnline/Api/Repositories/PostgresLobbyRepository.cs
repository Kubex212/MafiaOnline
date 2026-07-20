using Api.Data;
using Api.Data.Entities;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Api.Repositories;
public class PostgresLobbyRepository : ILobbyRepository
{
    private readonly MafiaDbContext _db;

    public PostgresLobbyRepository(MafiaDbContext db)
    {
        _db = db;
    }

    public async Task Create(Lobby lobby)
    {
        var entity = new LobbyEntity
        {
            Id = lobby.Id,
            Code = lobby.Code,
            StateJson = JsonConvert.SerializeObject(lobby),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Lobbies.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<Lobby?> Get(string code)
    {
        var entity = await _db.Lobbies
            .SingleOrDefaultAsync(x => x.Code == code);

        if (entity == null)
            return null;

        return JsonConvert.DeserializeObject<Lobby>(entity.StateJson);
    }

    public async Task Update(Lobby lobby)
    {
        var entity = await _db.Lobbies
            .SingleOrDefaultAsync(x => x.Id == lobby.Id);

        if (entity == null)
            throw new InvalidOperationException("Lobby not found.");

        entity.StateJson = JsonConvert.SerializeObject(lobby);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
