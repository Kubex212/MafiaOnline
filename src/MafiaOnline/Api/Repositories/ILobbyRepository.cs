using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Repositories;

public interface ILobbyRepository
{
    void Create(Lobby lobby);

    Lobby? Get(string code);

    void Update(Lobby lobby);
}