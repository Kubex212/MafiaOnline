using MafiaOnline.Api.Models;
using MafiaOnline.Api.Dtos;
using MafiaOnline.Engine.Roles;
using Api.Dtos;

namespace MafiaOnline.Api.Services;

public interface ILobbyService
{
    Task<Lobby> CreateLobby(string hostNickname);
    Task<Lobby> JoinLobbyAsync(string code, string nickname);
    Task<Lobby> GetLobby(string code);
    Task<Lobby> StartLobbyAsync(string code);
    Task<Lobby> UpdateConfigAsync(string code, GameConfigDto configDto);
    Task<RoleResponse> GetPlayerRole(string code);
    Task<Lobby> MovePlayerUpAsync(string code, Guid targetPlayerId);
    Task<Lobby> MovePlayerDownAsync(string code, Guid targetPlayerId);
    Task<Lobby> ShufflePlayersAsync(string code);
    Task<ReconnectResponse> ReconnectPlayerAsync(string code);
    Task<Lobby> CreateRematchAsync(string code);
    Task<IReadOnlyList<LobbyGameHistoryItemResponse>> GetLobbyHistoryAsync(string code, int take = 20);
    Task<Lobby?> LeaveLobbyAsync(string code);
    Task<Lobby?> KickPlayerAsync(string code, Guid targetPlayerId);
    Task DeleteLobbyAsync(string code);
    Task MarkReadyForRematchAsync(string code);
}