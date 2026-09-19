using Api.Data;
using Api.Data.Entities;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Api.Repositories;
public class PostgresLobbyRepository : ILobbyRepository
{
    private readonly MafiaDbContext _db;
    private readonly JsonSerializerSettings _settings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        Converters = { new StringEnumConverter() },
        ObjectCreationHandling = ObjectCreationHandling.Replace,
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
            lobby.TouchActivity();

            var entity = new LobbyEntity
            {
                Id = lobby.Id,
                Code = lobby.Code,
                StateJson = JsonConvert.SerializeObject(lobby, _settings),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastActivityAtUtc = lobby.LastActivityAtUtc
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
                lobby.CurrentGame?.Initialize();

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
                lobby.TouchActivity();

                entity.StateJson = JsonConvert.SerializeObject(lobby, _settings);
                entity.UpdatedAtUtc = DateTime.UtcNow;
                entity.LastActivityAtUtc = lobby.LastActivityAtUtc;

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

    public async Task Delete(string code)
    {
        try
        {
            var entity = await _db.Lobbies.SingleOrDefaultAsync(x => x.Code == code);
            if (entity == null) return;
            _db.Lobbies.Remove(entity);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lobby with code {LobbyCode}", code);
            throw;
        }
    }

    public async Task<IReadOnlyList<Lobby>> GetAll(int skip = 0, int take = 100)
    {
        try
        {
            var entities = await _db.Lobbies
                .OrderBy(e => e.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return DeserializeEntities(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lobbies page skip={Skip} take={Take}", skip, take);
            throw;
        }
    }

    public async Task<IReadOnlyList<Lobby>> GetAvailableAsync(DateTime activeSince, int skip = 0, int take = 100)
    {
        try
        {
            var entities = await _db.Lobbies
                .Where(e => e.LastActivityAtUtc >= activeSince)
                .OrderBy(e => e.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var lobbies = DeserializeEntities(entities);
            // Include lobbies that are waiting OR have a game in progress (spectating is allowed).
            // Exclude private lobbies and lobbies where the game has already finished (pre-rematch limbo).
            return lobbies.Where(l => l.Players.Count > 0 && !l.IsPrivate
                && l.CurrentGame?.Phase != MafiaOnline.Engine.GamePhase.Finished).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available lobbies activeSince={ActiveSince}", activeSince);
            throw;
        }
    }

    public async Task<IReadOnlyList<Lobby>> GetInactiveAsync(DateTime cutoff)
    {
        try
        {
            var entities = await _db.Lobbies
                .Where(e => e.LastActivityAtUtc < cutoff)
                .ToListAsync();

            return DeserializeEntities(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inactive lobbies cutoff={Cutoff}", cutoff);
            throw;
        }
    }

    private List<Lobby> DeserializeEntities(IEnumerable<LobbyEntity> entities)
    {
        var list = new List<Lobby>();
        foreach (var entity in entities)
        {
            try
            {
                var lobby = JsonConvert.DeserializeObject<Lobby>(entity.StateJson, _settings)!;
                lobby.CurrentGame?.Initialize();
                list.Add(lobby);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize lobby JSON for lobby entity {EntityId}, code={LobbyCode}", entity.Id, entity.Code);
            }
        }
        return list;
    }
}
