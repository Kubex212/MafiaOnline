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
    private readonly INightService _nightService;

    public LobbyService(ILobbyRepository repository, IHubContext<LobbyHub> hubContext, INightService nightService)
    {
        _repository = repository;
        _hubContext = hubContext;
        _nightService = nightService;
    }

    public async Task<Lobby> CreateLobby(string hostNickname)
    {
        if (string.IsNullOrWhiteSpace(hostNickname))
            throw new ArgumentException("Nickname is required.", nameof(hostNickname));

        Lobby lobby;

        do
        {
            var code = LobbyCodeGenerator.Generate();
            lobby = new Lobby(code);
        }
        while (await _repository.Get(lobby.Code) != null);

        lobby.AddPlayer(new LobbyPlayer(hostNickname.Trim(), true));

        await _repository.Create(lobby);

        return lobby;
    }

    public async Task<Lobby> JoinLobbyAsync(string code, string nickname)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("Nickname is required.", nameof(nickname));

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        lobby.AddPlayer(new LobbyPlayer(nickname.Trim(), false));

        await _hubContext.Clients
            .Group(lobby.Code)
            .SendAsync("PlayerJoined", nickname);

        await _repository.Update(lobby);

        return lobby;
    }

    public async Task<Lobby> GetLobby(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        return await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");
    }

    public async Task<Lobby> StartLobbyAsync(string code, Guid requestingPlayerId)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        lobby.Start(requestingPlayerId);
        await _repository.Update(lobby);

        // Broadcast seat list (no roles) to all players in the group
        var seats = lobby.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new { id = p.Id, nickname = p.Nickname, seatIndex = p.SeatIndex })
            .ToList();

        await _hubContext.Clients
            .Group(lobby.Code)
            .SendAsync("GameStarted", seats);

        // Give clients a moment to render before the first turn fires
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            await _nightService.BeginNightAsync(lobby.Code);
        });

        return lobby;
    }

    public async Task<string> GetPlayerRole(string code, Guid playerId)
    {
        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        if (!lobby.Started || lobby.Game is null)
            throw new InvalidOperationException("Game has not started yet.");

        var enginePlayer = lobby.Game.Players.FirstOrDefault(p => p.Id == playerId)
            ?? throw new InvalidOperationException("Player not found.");

        return enginePlayer.Role.Name;
    }

    public async Task<Lobby> MovePlayerUpAsync(string code, Guid requestingPlayerId, Guid targetPlayerId)
        => await MutateOrderAsync(code, requestingPlayerId, lobby => lobby.MovePlayerUp(targetPlayerId));

    public async Task<Lobby> MovePlayerDownAsync(string code, Guid requestingPlayerId, Guid targetPlayerId)
        => await MutateOrderAsync(code, requestingPlayerId, lobby => lobby.MovePlayerDown(targetPlayerId));

    public async Task<Lobby> ShufflePlayersAsync(string code, Guid requestingPlayerId)
        => await MutateOrderAsync(code, requestingPlayerId, lobby => lobby.ShufflePlayers());

    private async Task<Lobby> MutateOrderAsync(string code, Guid requestingPlayerId, Action<Lobby> mutation)
    {
        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var requester = lobby.Players.FirstOrDefault(p => p.Id == requestingPlayerId)
            ?? throw new InvalidOperationException("Player not found in lobby.");

        if (!requester.IsHost)
            throw new InvalidOperationException("Only the host can reorder players.");

        mutation(lobby);
        await _repository.Update(lobby);

        // Broadcast the new order to everyone in the lobby
        var orderedPlayers = lobby.Players
            .Select((p, i) => new { id = p.Id, nickname = p.Nickname, isHost = p.IsHost, lobbyPosition = i })
            .ToList();

        await _hubContext.Clients
            .Group(lobby.Code)
            .SendAsync("LobbyOrderChanged", orderedPlayers);

        return lobby;
    }
}