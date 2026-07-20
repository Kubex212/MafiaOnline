using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Services;

public interface ILobbyService
{
    Task<Lobby> CreateLobby(string hostNickname);

    Task<Lobby> JoinLobbyAsync(string code, string nickname);

    Task<Lobby> GetLobby(string code);

    Task<Lobby> StartLobbyAsync(string code, Guid requestingPlayerId);

    Task<string> GetPlayerRole(string code, Guid playerId);
    Task<Lobby> MovePlayerUpAsync(string code, Guid requestingPlayerId, Guid targetPlayerId);
    Task<Lobby> MovePlayerDownAsync(string code, Guid requestingPlayerId, Guid targetPlayerId);
    Task<Lobby> ShufflePlayersAsync(string code, Guid requestingPlayerId);
}