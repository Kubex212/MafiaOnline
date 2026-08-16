using MafiaOnline.Api.Dtos;
using MafiaOnline.Api.Hubs;
using MafiaOnline.Api.Identity;
using MafiaOnline.Api.Models;
using MafiaOnline.Api.Repositories;
using MafiaOnline.Api.Utils;
using MafiaOnline.Engine;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using static MafiaOnline.Engine.Game;

namespace MafiaOnline.Api.Services;

public class LobbyService : ILobbyService
{
    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hubContext;
    private readonly INightService _nightService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LobbyService> _logger;
    private readonly ICurrentUser _currentUser;

    public LobbyService(ILobbyRepository repository, IHubContext<LobbyHub> hubContext, INightService nightService, IServiceScopeFactory scopeFactory, ILogger<LobbyService> logger, ICurrentUser currentUser)
    {
        _repository = repository;
        _hubContext = hubContext;
        _nightService = nightService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _currentUser = currentUser;
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

        lobby.AddPlayer(new LobbyPlayer(_currentUser.UserId, hostNickname.Trim(), true));

        await _repository.Create(lobby);

        _logger.LogInformation("Created lobby {LobbyCode} hostUser={UserId} hostNick={Nickname}", lobby.Code, _currentUser.UserId, hostNickname);

        return lobby;
    }

    public async Task<Lobby> CreateRematchAsync(string code)
    {
        _logger.LogInformation("CreateRematchAsync called for lobby={LobbyCode} by user={UserId}", code, _currentUser.UserId);

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        // Load existing lobby to copy config from
        var oldLobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        // Generate a new unique code
        Lobby newLobby;
        do
        {
            var newCode = LobbyCodeGenerator.Generate();
            newLobby = new Lobby(newCode);
        }
        while (await _repository.Get(newLobby.Code) != null);

        // Copy configuration from existing lobby
        newLobby.UpdateConfig(oldLobby.Config);

        // Add current user as host
        // Use the nickname from existing lobby if the same user was in original lobby, otherwise generic
        var hostNick = oldLobby.Players.FirstOrDefault(p => p.UserId == _currentUser.UserId)?.Nickname ?? "Host";
        newLobby.AddPlayer(new LobbyPlayer(_currentUser.UserId, hostNick.Trim(), true));

        // set rematch code in old lobby in case someone reconnects 
        oldLobby.RematchCode = newLobby.Code;
        await _repository.Update(oldLobby);

        await _repository.Create(newLobby);

        _logger.LogInformation("Created rematch lobby {LobbyCode} from {OldCode} by user={UserId}", newLobby.Code, oldLobby.Code, _currentUser.UserId);

        // Notify players in the old lobby that a rematch has been created
        try
        {
            await _hubContext.Clients
                .Group(oldLobby.Code)
                .SendAsync("RematchCreated", newLobby.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify old lobby {OldCode} about rematch {NewCode}", oldLobby.Code, newLobby.Code);
        }

        return newLobby;
    }

    public async Task<Lobby> JoinLobbyAsync(string code, string nickname)
    {
        _logger.LogInformation("JoinLobbyAsync called code={Code} nickname={Nickname} user={UserId}", code, nickname, _currentUser.UserId);

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("Nickname is required.", nameof(nickname));

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        lobby.AddPlayer(new LobbyPlayer(_currentUser.UserId, nickname.Trim(), false));

        await _repository.Update(lobby);
        await _hubContext.Clients
            .Group(lobby.Code)
            .SendAsync("PlayerJoined", nickname);

        _logger.LogInformation("Player {Nickname} joined lobby {LobbyCode} user={UserId}", nickname, lobby.Code, _currentUser.UserId);

        return lobby;
    }

    public async Task<Lobby> GetLobby(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        return await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");
    }

    public async Task<Lobby> StartLobbyAsync(string code)
    {
        _logger.LogInformation("StartLobbyAsync called for code={LobbyCode} by user={UserId}", code, _currentUser.UserId);

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        var userId = _currentUser.UserId;

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())!
            ?? throw new InvalidOperationException("Lobby not found.");

        var requestingPlayer = GetPlayerByUserId(lobby, userId);
        lobby.Start(requestingPlayer.Id);
        await _repository.Update(lobby);

        _logger.LogInformation("Lobby {LobbyCode} started by player {PlayerId}", lobby.Code, requestingPlayer.Id);

        // broadcast seat list (no roles) to all players in the group
        var seats = lobby.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new { id = p.Id, nickname = p.Nickname, seatIndex = p.SeatIndex })
            .ToList();

        try
        {
            var drunkPresent = lobby.Game!.DrunkPlayer != null && lobby.Game.Config?.Drunk?.AnnounceDrunkOccurance == true;
            await _hubContext.Clients
                .Group(lobby.Code)
                .SendAsync("GameStarted", new { seats, drunkPresent });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast GameStarted for lobby {LobbyCode}", lobby.Code);
        }

        // give clients a moment to see their roles before the first turn fires
        try
        {
            await Task.Delay(8000);
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

    public async Task<RoleResponse> GetPlayerRole(string code)
    {
        _logger.LogDebug("GetPlayerRole called for lobby={LobbyCode} user={UserId}", code, _currentUser.UserId);

        var userId = _currentUser.UserId;

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        if (!lobby.Started || lobby.Game is null)
            throw new InvalidOperationException("Game has not started yet.");

        var player = GetPlayerByUserId(lobby, userId);
        var enginePlayer = lobby.Game.Players.FirstOrDefault(p => p.Id == player.Id)
            ?? throw new InvalidOperationException("Player not found.");

        var response = new RoleResponse()
        {
            Name = enginePlayer.Role.Name,
            TeamName = enginePlayer.Role.Team.ToString(),
            TeamId = (int)enginePlayer.Role.Team,
            Alibi = enginePlayer.Alibi.ToString()
        };

        var isWerewolf = enginePlayer.Role.Team == Team.Werewolves;
        if (isWerewolf)
        {
            response.OtherWerewolvesIds = lobby.Game.Players
                .Where(p => p.Role.Team == Team.Werewolves && p.Id != player.Id)
                .Select(p => p.Id)
                .ToList();
        }
        return response;
    }

    public async Task<Lobby> MovePlayerUpAsync(string code, Guid targetPlayerId)
        => await MutateOrderAsync(code, lobby => lobby.MovePlayerUp(targetPlayerId));

    public async Task<Lobby> MovePlayerDownAsync(string code, Guid targetPlayerId)
        => await MutateOrderAsync(code, lobby => lobby.MovePlayerDown(targetPlayerId));

    public async Task<Lobby> ShufflePlayersAsync(string code)
        => await MutateOrderAsync(code, lobby => lobby.ShufflePlayers());

    private async Task<Lobby> MutateOrderAsync(string code, Action<Lobby> mutation)
    {
        _logger.LogInformation("MutateOrderAsync called for lobby={LobbyCode} user={UserId}", code, _currentUser.UserId);

        var userId = _currentUser.UserId;

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var requester = GetPlayerByUserId(lobby, userId);

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

        _logger.LogInformation("Lobby order changed for {LobbyCode} by {UserId}", lobby.Code, userId);

        return lobby;
    }

    public async Task<ReconnectResponse> ReconnectPlayerAsync(string code)
    {
        _logger.LogInformation("ReconnectPlayerAsync called for lobby={LobbyCode} user={UserId}", code, _currentUser.UserId);

        var userId = _currentUser.UserId;

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var player = GetPlayerByUserId(lobby, userId);

        var response = new ReconnectResponse
        {
            Lobby = LobbyResponse.FromLobby(lobby),
            GamePhase = "lobby",
            MyPlayerId = player.Id,
        };

        if (!lobby.Started)
        {
            return response;
        }

        // Game has started - determine current phase and provide game state
        if (lobby.Game != null)
        {
            var enginePlayer = lobby.Game.Players.FirstOrDefault(p => p.Id == player.Id);
            if (enginePlayer != null)
            {
                var myRoleResponse = new RoleResponse()
                {
                    Name = enginePlayer.Role.Name,
                    TeamName = enginePlayer.Role.Team.ToString(),
                    TeamId = (int)enginePlayer.Role.Team,
                    Alibi = enginePlayer.Alibi.ToString()
                };

                var isWerewolf = enginePlayer.Role.Team == Team.Werewolves;
                if (isWerewolf)
                {
                    myRoleResponse.OtherWerewolvesIds = lobby.Game.Players
                        .Where(p => p.Role.Team == Team.Werewolves && p.Id != player.Id)
                        .Select(p => p.Id)
                        .ToList();
                }
                response.MyRole = myRoleResponse;
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
                RemainingSeconds = (int)(DateTime.UtcNow - lobby.NightSession.CurrentTurnStart).TotalSeconds,
                AlivePlayers = lobby.Players.Where(p => lobby.Game!.Players.First(ep => ep.Id == p.Id).IsAlive)
                    .OrderBy(p => p.SeatIndex)
                    .Select(p => new SeatInfoResponse { Id = p.Id, Nickname = p.Nickname, SeatIndex = p.SeatIndex })
                    .ToList(),
                SubmittedActions = null
            };
        }
        else if (lobby.DaySession != null && !lobby.DaySession.Resolved)
        {
            response.GamePhase = "day";

            var enshrouded = new List<Guid>();
            var last = lobby.Game?.LastNightResult;
            if (last != null)
            {
                foreach (var ev in last.Events)
                {
                    if (ev is DraculaEnshroudedEvent d)
                        enshrouded.Add(d.TargetId);
                }
            }

            var asleep = new List<Guid>();
            if (last != null)
            {
                foreach (var ev in last.Events)
                {
                    if (ev is CharmerSleepEvent c)
                        asleep.Add(c.TargetId);
                }
            }

            response.Day = new DayState
            {
                DurationSeconds = lobby.Game!.Config.Day.DurationSeconds,
                RemainingSeconds = 0,
                ShowVoteTargets = lobby.Game!.Config.Day.ShowVoteTargets,
                AlivePlayers = lobby.Players.Where(p => lobby.Game!.Players.First(ep => ep.Id == p.Id).IsAlive)
                    .OrderBy(p => p.SeatIndex)
                    .Select(p => new SeatInfoResponse { Id = p.Id, Nickname = p.Nickname, SeatIndex = p.SeatIndex })
                    .ToList(),
                Votes = lobby.DaySession.Votes.ToDictionary(k => k.Key, v => v.Value),
                Enshrouded = enshrouded,
                Asleep = asleep,
            };
        }
        else if (lobby.Game?.Phase == GamePhase.Finished)
        {
            response.GamePhase = "gameover";
            response.GameOver = new GameOverPayload
            {
                Winner = lobby.Game.Winner.ToString(),
                DrunkId = lobby.Game.DrunkPlayer?.Id,
                DrunkName = lobby.Game.DrunkPlayer?.Nickname,
                RematchCode = lobby.RematchCode
            };
        }

        return response;
    }

    public async Task<Lobby> UpdateConfigAsync(string code, GameConfigDto configDto)
    {
        _logger.LogInformation("UpdateConfigAsync called for lobby={LobbyCode} user={UserId}", code, _currentUser.UserId);

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        var userId = _currentUser.UserId;

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var requester = GetPlayerByUserId(lobby, userId);

        if (!requester.IsHost)
            throw new InvalidOperationException("Only the host can change game configuration.");

        lobby.UpdateConfig(configDto.ToConfig());
        await _repository.Update(lobby);

        await _hubContext.Clients
            .Group(lobby.Code)
            .SendAsync("ConfigUpdated", configDto);

        _logger.LogInformation("ConfigUpdated for lobby {LobbyCode} by {UserId}", lobby.Code, userId);

        return lobby;
    }

    private static LobbyPlayer GetPlayerByUserId(Lobby lobby, Guid userId)
        => lobby.Players.FirstOrDefault(p => p.UserId == userId)
           ?? throw new InvalidOperationException("Player not found in lobby.");
}