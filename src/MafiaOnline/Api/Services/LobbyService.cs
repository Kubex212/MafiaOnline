using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Api.Utils;
using MafiaOnline.Api.Dtos;
using MafiaOnline.Engine;
using Microsoft.AspNetCore.SignalR;
using static MafiaOnline.Engine.Game;

namespace MafiaOnline.Api.Services;

public class LobbyService : ILobbyService
{
    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hubContext;
    private readonly INightService _nightService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LobbyService> _logger;

    public LobbyService(ILobbyRepository repository, IHubContext<LobbyHub> hubContext, INightService nightService, IServiceScopeFactory scopeFactory, ILogger<LobbyService> logger)
    {
        _repository = repository;
        _hubContext = hubContext;
        _nightService = nightService;
        _scopeFactory = scopeFactory;
        _logger = logger;
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
        try
        {
            await Task.Delay(2000);
            using var scope = _scopeFactory.CreateScope();
            var nightService = scope.ServiceProvider.GetRequiredService<INightService>();
            await nightService.BeginNightAsync(lobby.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting night phase for lobby {LobbyCode}", lobby.Code);
        }

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

    public async Task<ReconnectResponse> ReconnectPlayerAsync(string code, Guid playerId)
    {
        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var player = lobby.Players.FirstOrDefault(p => p.Id == playerId)
            ?? throw new InvalidOperationException("Player not found in lobby.");

        var response = new ReconnectResponse
        {
            Lobby = LobbyResponse.FromLobby(lobby),
            GamePhase = "lobby"
        };

        if (!lobby.Started)
        {
            return response;
        }

        // Game has started - determine current phase and provide game state
        if (lobby.Game != null)
        {
            var enginePlayer = lobby.Game.Players.FirstOrDefault(p => p.Id == playerId);
            if (enginePlayer != null)
            {
                response.MyRole = new RoleResponse
                {
                    Name = enginePlayer.Role.Name,
                    Team = enginePlayer.Role.Name is "Werewolf" ? "Werewolf" : "Town"
                };
            }

            // Map players to seat info for the frontend
            response.Seats = lobby.Players
                .OrderBy(p => p.SeatIndex)
                .Select(p => new SeatInfoResponse
                {
                    Id = p.Id,
                    Nickname = p.Nickname,
                    SeatIndex = p.SeatIndex
                })
                .ToList();
        }

        // Determine current game phase
        if (lobby.NightSession != null && !lobby.NightSession.Resolved)
        {
            response.GamePhase = "night";

            response.Night = new NightState
            {
                ActivePlayerId = lobby.NightSession.ActivePlayer?.Id.ToString(),
                ActiveSeatIndex = lobby.NightSession.ActivePlayer?.SeatIndex ?? 0,
                HasAbility = lobby.ActivePlayerHasAbility(),
                RemainingSeconds = 0,
                SubmittedActions = lobby.NightSession.SubmittedActions.ToDictionary(k => k.Key, v => v.Value)
            };
        }
        else if (lobby.DaySession != null && !lobby.DaySession.Resolved)
        {
            response.GamePhase = "day";

            response.Day = new DayState
            {
                DurationSeconds = lobby.Game?.Config.Day.DurationSeconds ?? 180,
                RemainingSeconds = 0,
                ShowVoteTargets = lobby.Game?.Config.Day.ShowVoteTargets ?? false,
                AlivePlayers = lobby.Players.Where(p => lobby.Game.Players.First(ep => ep.Id == p.Id).IsAlive)
                    .OrderBy(p => p.SeatIndex)
                    .Select(p => new SeatInfoResponse { Id = p.Id, Nickname = p.Nickname, SeatIndex = p.SeatIndex })
                    .ToList(),

                Votes = lobby.DaySession.Votes.ToDictionary(k => k.Key, v => v.Value)
            };
        }
        else if (lobby.Game?.Phase == GamePhase.Finished)
        {
            response.GamePhase = "gameover";
        }

        return response;
    }
}