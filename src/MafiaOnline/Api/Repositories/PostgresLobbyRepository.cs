using Api.Data;
using Api.Data.Entities;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Api.Repositories;
public class PostgresLobbyRepository : ILobbyRepository
{
    private readonly MafiaDbContext _db;
    private readonly JsonSerializerSettings _settings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto
    };
    private readonly ILogger<PostgresLobbyRepository> _logger;


    public PostgresLobbyRepository(MafiaDbContext db, ILogger<PostgresLobbyRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Create(Lobby lobby)
    {
        try
        {
            var entity = new LobbyEntity
            {
                Id = lobby.Id,
                Code = lobby.Code,
                StateJson = JsonConvert.SerializeObject(lobby, _settings),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Lobbies.Add(entity);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create lobby entity for lobby {LobbyId} code={LobbyCode}", lobby.Id, lobby.Code);
            throw;
        }
    }

    public async Task<Lobby?> Get(string code)
    {
        try
        {
            var entity = await _db.Lobbies
                .SingleOrDefaultAsync(x => x.Code == code);

            if (entity == null)
                return null;

            try
            {
                var lobby = JsonConvert.DeserializeObject<Lobby>(entity.StateJson, _settings)!;
                lobby.Game?.Initialize();

                return lobby;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize lobby JSON for lobby entity {EntityId}, code={LobbyCode}, json={Json}", entity.Id, code, entity.StateJson);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lobby with code {LobbyCode}", code);
            throw;
        }
    }

    public async Task Update(Lobby lobby)
    {
        try
        {
            var entity = await _db.Lobbies
                .SingleOrDefaultAsync(x => x.Id == lobby.Id);

            if (entity == null)
                throw new InvalidOperationException($"Lobby {lobby.Id} not found.");

            try
            {
                entity.StateJson = JsonConvert.SerializeObject(lobby, _settings);
                entity.UpdatedAtUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize or save lobby {LobbyId} code={LobbyCode}", lobby.Id, lobby.Code);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lobby {LobbyId}", lobby.Id);
            throw;
        }
    }
}
