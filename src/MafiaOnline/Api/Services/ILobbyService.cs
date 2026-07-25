using MafiaOnline.Api.Models;
using MafiaOnline.Api.Dtos;
using MafiaOnline.Engine.Roles;

namespace MafiaOnline.Api.Services;

public interface ILobbyService
{
    Task<Lobby> CreateLobby(string hostNickname);

    Task<Lobby> JoinLobbyAsync(string code, string nickname);

    Task<Lobby> GetLobby(string code);

    Task<Lobby> StartLobbyAsync(string code, Guid requestingPlayerId);

    Task<Lobby> UpdateConfigAsync(string code, Guid requestingPlayerId, GameConfigDto configDto);

    Task<RoleResponse> GetPlayerRole(string code, Guid playerId);
    Task<Lobby> MovePlayerUpAsync(string code, Guid requestingPlayerId, Guid targetPlayerId);
    Task<Lobby> MovePlayerDownAsync(string code, Guid requestingPlayerId, Guid targetPlayerId);
    Task<Lobby> ShufflePlayersAsync(string code, Guid requestingPlayerId);
    Task<ReconnectResponse> ReconnectPlayerAsync(string code, Guid playerId);
}