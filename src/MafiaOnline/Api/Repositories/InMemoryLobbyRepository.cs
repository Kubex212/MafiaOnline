using System.Collections.Concurrent;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Utils;

namespace MafiaOnline.Api.Repositories;

public class InMemoryLobbyRepository : ILobbyRepository
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();

    public void Create(Lobby lobby)
    {
        if (!_lobbies.TryAdd(lobby.Code, lobby))
            throw new InvalidOperationException("A lobby with this code already exists.");
    }

    public Lobby? Get(string code)
    {
        _lobbies.TryGetValue(code.ToUpperInvariant(), out var lobby);

        return lobby;
    }

    public void Update(Lobby lobby)
    {
        _lobbies[lobby.Code] = lobby;
    }
}