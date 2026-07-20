using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Repositories;

public interface ILobbyRepository
{
    Task Create(Lobby lobby);
    Task<Lobby?> Get(string code);
    Task Update(Lobby lobby);
}