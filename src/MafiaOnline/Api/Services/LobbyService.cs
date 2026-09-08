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
using Api.Dtos;
using Api.Services;

namespace MafiaOnline.Api.Services;

public class LobbyService : ILobbyService
{
    private readonly ILobbyRepository _repository;
    private readonly IHubContext<LobbyHub> _hubContext;
    private readonly INightService _nightService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LobbyService> _logger;
    private readonly ICurrentUser _currentUser;
    private readonly IGameHistoryService _gameHistoryService;

    public LobbyService(ILobbyRepository repository, IHubContext<LobbyHub> hubContext, INightService nightService, IServiceScopeFactory scopeFactory, ILogger<LobbyService> logger, ICurrentUser currentUser, IGameHistoryService gameHistoryService)
    {
        _repository = repository;
        _hubContext = hubContext;
        _nightService = nightService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _currentUser = currentUser;
        _gameHistoryService = gameHistoryService;
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

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var requester = GetPlayerByUserId(lobby, _currentUser.UserId);
        if (!requester.IsHost)
            throw new InvalidOperationException("Only the host can create a rematch.");

        if (lobby.CurrentGame?.Phase != GamePhase.Finished)
            throw new InvalidOperationException("Rematch is only available after the game ends.");

        lobby.ResetForNextGame();
        await _repository.Update(lobby);

        _logger.LogInformation("Reset lobby {LobbyCode} for rematch by user={UserId}", lobby.Code, _currentUser.UserId);

        try
        {
            await _hubContext.Clients
                .Group(lobby.Code)
                .SendAsync("RematchCreated", lobby.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify lobby {LobbyCode} about rematch reset", lobby.Code);
        }

        return lobby;
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

        var existingPlayer = lobby.Players.FirstOrDefault(x => x.UserId == _currentUser.UserId);

        if (existingPlayer != null)
            return lobby;

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

        _logger.LogInformation("Lobby {LobbyCode} started by player {PlayerId} game={GameId}", lobby.Code, requestingPlayer.Id, lobby.CurrentGame!.Id);

        var seats = lobby.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new { id = p.Id, nickname = p.Nickname, seatIndex = p.SeatIndex })
            .ToList();

        try
        {
            var drunkPresent = lobby.CurrentGame!.DrunkPlayer != null && lobby.CurrentGame.Config?.Drunk?.AnnounceDrunkOccurance == true;
            await _hubContext.Clients
                .Group(lobby.Code)
                .SendAsync("GameStarted", new { gameId = lobby.CurrentGame.Id, seats, drunkPresent, config = lobby.Config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast GameStarted for lobby {LobbyCode}", lobby.Code);
        }

        try
        {
            var gameId = lobby.CurrentGame!.Id;
            await Task.Delay(8000);
            using var scope = _scopeFactory.CreateScope();
            var refreshed = await scope.ServiceProvider.GetRequiredService<ILobbyRepository>().Get(lobby.Code);
            if (refreshed?.CurrentGame?.Id == gameId)
            {
                var nightService = scope.ServiceProvider.GetRequiredService<INightService>();
                await nightService.BeginNightAsync(lobby.Code);
            }
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

        if (!lobby.Started || lobby.CurrentGame is null)
            throw new InvalidOperationException("Game has not started yet.");

        var player = GetPlayerByUserId(lobby, userId);
        var enginePlayer = lobby.CurrentGame.Players.FirstOrDefault(p => p.Id == player.Id)
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
            response.OtherWerewolvesIds = lobby.CurrentGame.Players
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

        if (!lobby.Started || lobby.CurrentGame is null)
            return response;

        var currentGame = lobby.CurrentGame;
        var enginePlayer = currentGame.Players.FirstOrDefault(p => p.Id == player.Id);
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
                myRoleResponse.OtherWerewolvesIds = currentGame.Players
                    .Where(p => p.Role.Team == Team.Werewolves && p.Id != player.Id)
                    .Select(p => p.Id)
                    .ToList();
            }
            response.MyRole = myRoleResponse;
        }

        response.Seats = lobby.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new SeatInfoResponse
            {
                Id = p.Id,
                Nickname = p.Nickname,
                SeatIndex = p.SeatIndex
            })
            .ToList();

        if (lobby.NightSession != null && !lobby.NightSession.Resolved)
        {
            response.GamePhase = "night";

            response.Night = new NightState
            {
                ActivePlayerId = lobby.NightSession.ActivePlayer?.Id.ToString(),
                ActiveSeatIndex = lobby.NightSession.ActivePlayer?.SeatIndex ?? 0,
                HasAbility = lobby.ActivePlayerHasAbility(),
                RemainingSeconds = Math.Max(0, currentGame.Config.TurnDurationSeconds - (int)(DateTime.UtcNow - lobby.NightSession.CurrentTurnStart).TotalSeconds),
                AlivePlayers = lobby.Players.Where(p => currentGame.Players.First(ep => ep.Id == p.Id).IsAlive)
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
            var last = currentGame.LastNightResult;
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
                DurationSeconds = currentGame.Config.Day.DurationSeconds,
                RemainingSeconds = 0,
                ShowVoteTargets = currentGame.Config.Day.ShowVoteTargets,
                AlivePlayers = lobby.Players.Where(p => currentGame.Players.First(ep => ep.Id == p.Id).IsAlive)
                    .OrderBy(p => p.SeatIndex)
                    .Select(p => new SeatInfoResponse { Id = p.Id, Nickname = p.Nickname, SeatIndex = p.SeatIndex })
                    .ToList(),
                Votes = lobby.DaySession.Votes.ToDictionary(k => k.Key, v => v.Value),
                Enshrouded = enshrouded,
                Asleep = asleep,
            };
        }
        else if (currentGame.Phase == GamePhase.Finished)
        {
            response.GamePhase = "gameover";
            response.GameOver = new GameOverPayload
            {
                Winner = currentGame.Winner.ToString(),
                DrunkId = currentGame.DrunkPlayer?.Id,
                DrunkName = currentGame.DrunkPlayer?.Nickname,
                RematchCode = lobby.RematchCode ?? lobby.Code
            };
        }

        return response;
    }

    public async Task<Lobby?> LeaveLobbyAsync(string code)
    {
        _logger.LogInformation("LeaveLobbyAsync called for lobby={LobbyCode} by user={UserId}", code, _currentUser.UserId);

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var player = lobby.Players.FirstOrDefault(p => p.UserId == _currentUser.UserId);
        if (player == null) throw new InvalidOperationException("Player not found in lobby.");

        lobby.RemovePlayerByUserId(_currentUser.UserId);

        if (lobby.Players.Count == 0)
        {
            await _repository.Delete(lobby.Code);
            try { await _hubContext.Clients.Group(lobby.Code).SendAsync("PlayerLeft", player.Nickname); } catch { }
            return null;
        }

        await _repository.Update(lobby);

        try
        {
            await _hubContext.Clients
                .Group(lobby.Code)
                .SendAsync("PlayerLeft", player.Nickname);

            var orderedPlayers = lobby.Players
                .Select((p, i) => new { id = p.Id, nickname = p.Nickname, isHost = p.IsHost, lobbyPosition = i })
                .ToList();

            await _hubContext.Clients.Group(lobby.Code).SendAsync("LobbyOrderChanged", orderedPlayers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify lobby {LobbyCode} about player leaving", lobby.Code);
        }

        return lobby;
    }

    public async Task<Lobby?> KickPlayerAsync(string code, Guid targetPlayerId)
    {
        _logger.LogInformation("KickPlayerAsync called for lobby={LobbyCode} by user={UserId} targetPlayer={Target}", code, _currentUser.UserId, targetPlayerId);

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var requester = GetPlayerByUserId(lobby, _currentUser.UserId);
        if (!requester.IsHost)
            throw new InvalidOperationException("Only the host can kick players.");

        var target = lobby.Players.FirstOrDefault(p => p.Id == targetPlayerId);
        if (target == null) throw new InvalidOperationException("Target player not found in lobby.");

        lobby.RemovePlayerById(targetPlayerId);

        if (lobby.Players.Count == 0)
        {
            await _repository.Delete(lobby.Code);
            try { await _hubContext.Clients.Group(lobby.Code).SendAsync("PlayerKicked", target.Nickname); } catch { }
            return null;
        }

        await _repository.Update(lobby);

        try
        {
            await _hubContext.Clients.Group(lobby.Code).SendAsync("PlayerKicked", target.Nickname);

            var orderedPlayers = lobby.Players
                .Select((p, i) => new { id = p.Id, nickname = p.Nickname, isHost = p.IsHost, lobbyPosition = i })
                .ToList();

            await _hubContext.Clients.Group(lobby.Code).SendAsync("LobbyOrderChanged", orderedPlayers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify lobby {LobbyCode} about player kicked", lobby.Code);
        }

        return lobby;
    }

    public async Task DeleteLobbyAsync(string code)
    {
        _logger.LogInformation("DeleteLobbyAsync called for lobby={LobbyCode} by user={UserId}", code, _currentUser.UserId);
        await _repository.Delete(code.Trim().ToUpperInvariant());
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

    public async Task<IReadOnlyList<LobbyGameHistoryItemResponse>> GetLobbyHistoryAsync(string code, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        var lobby = await _repository.Get(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Lobby not found.");

        var games = await _gameHistoryService.GetLobbyGamesAsync(lobby.Id, take);

        return games.Select(x => new LobbyGameHistoryItemResponse
        {
            GameId = x.Id,
            LobbyCode = x.LobbyCode,
            StartedAtUtc = x.StartedAtUtc,
            FinishedAtUtc = x.FinishedAtUtc,
            Winner = x.Winner,
            PlayerCount = x.PlayerCount,
            DayCount = x.DayCount,
            Duration = x.Duration
        }).ToList();
    }

    private static LobbyPlayer GetPlayerByUserId(Lobby lobby, Guid userId)
        => lobby.Players.FirstOrDefault(p => p.UserId == userId)
           ?? throw new InvalidOperationException("Player not found in lobby.");
}