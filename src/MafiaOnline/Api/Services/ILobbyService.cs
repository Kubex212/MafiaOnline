using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Services;

public interface ILobbyService
{
    Lobby CreateLobby(string hostNickname);

    Task<Lobby> JoinLobbyAsync(string code, string nickname);

    Lobby GetLobby(string code);

    Task<Lobby> StartLobbyAsync(string code, Guid requestingPlayerId);

    string GetPlayerRole(string code, Guid playerId);
}