using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Repositories;

public interface ILobbyRepository
{
    Task Create(Lobby lobby);
    Task<Lobby?> Get(string code);
    Task Update(Lobby lobby);
    Task Delete(string code);

    // Return stored lobbies in pages (skip/take)
    Task<IReadOnlyList<Lobby>> GetAll(int skip = 0, int take = 100);
}