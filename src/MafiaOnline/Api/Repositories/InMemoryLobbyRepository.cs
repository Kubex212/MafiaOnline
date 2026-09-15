using System.Collections.Concurrent;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Utils;

namespace MafiaOnline.Api.Repositories;

public class InMemoryLobbyRepository : ILobbyRepository
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();

    public async Task Create(Lobby lobby)
    {
        if (!_lobbies.TryAdd(lobby.Code, lobby))
            throw new InvalidOperationException("A lobby with this code already exists.");
    }

    public async Task<Lobby?> Get(string code)
    {
        _lobbies.TryGetValue(code.ToUpperInvariant(), out var lobby);

        return lobby;
    }

    public async Task Update(Lobby lobby)
    {
        _lobbies[lobby.Code] = lobby;
    }

    public async Task Delete(string code)
    {
        _lobbies.TryRemove(code.ToUpperInvariant(), out _);
    }

    public async Task<IReadOnlyList<Lobby>> GetAll(int skip = 0, int take = 100)
    {
        return _lobbies.Values.OrderBy(l => l.Code).Skip(skip).Take(take).ToList();
    }
}