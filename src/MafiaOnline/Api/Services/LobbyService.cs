using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Api.Utils;
using Microsoft.AspNetCore.SignalR;

namespace MafiaOnline.Api.Services;

public class LobbyService : ILobbyService
{
    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hubContext;

    public LobbyService(ILobbyRepository repository, IHubContext<LobbyHub> hubContext)
    {
        _repository = repository;
        _hubContext = hubContext;
    }

    public Lobby CreateLobby(string hostNickname)
    {
        if (string.IsNullOrWhiteSpace(hostNickname))
            throw new ArgumentException("Nickname is required.", nameof(hostNickname));

        Lobby lobby;

        do
        {
            var code = LobbyCodeGenerator.Generate();
            lobby = new Lobby(code);
        }
        while (_repository.Get(lobby.Code) != null);

        lobby.AddPlayer(new LobbyPlayer(hostNickname.Trim(), true));

        _repository.Create(lobby);

        return lobby;
    }

    public async Task<Lobby> JoinLobbyAsync(string code, string nickname)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("Nickname is required.", nameof(nickname));

        var lobby = _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        lobby.AddPlayer(new LobbyPlayer(nickname.Trim(), false));

        await _hubContext.Clients
            .Group(lobby.Code)
            .SendAsync("PlayerJoined", nickname);

        _repository.Update(lobby);

        return lobby;
    }

    public Lobby GetLobby(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        return _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");
    }

    public async Task<Lobby> StartLobbyAsync(string code, Guid requestingPlayerId)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        var lobby = _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        lobby.Start(requestingPlayerId);
        _repository.Update(lobby);

        // Broadcast seat list (no roles) to all players in the group
        var seats = lobby.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new { p.Id, p.Nickname, p.SeatIndex })
            .ToList();

        await _hubContext.Clients
            .Group(lobby.Code)
            .SendAsync("GameStarted", seats);

        return lobby;
    }

    public string GetPlayerRole(string code, Guid playerId)
    {
        var lobby = _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        if (!lobby.Started || lobby.Game is null)
            throw new InvalidOperationException("Game has not started yet.");

        var enginePlayer = lobby.Game.Players.FirstOrDefault(p => p.Id == playerId)
            ?? throw new InvalidOperationException("Player not found.");

        return enginePlayer.Role.Name;
    }
}