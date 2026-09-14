using Api.Dtos;
using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;
using System;
using System.Collections.Generic;

namespace MafiaOnline.Api.Dtos;

public class ReconnectResponse
{
    public LobbyResponse Lobby { get; set; } = null!;

    public string GamePhase { get; set; } = "lobby";

    public Guid MyPlayerId { get; set; }

    public RoleResponse? MyRole { get; set; }

    public List<SeatInfoResponse>? Seats { get; set; }

    // Optional state for day/night
    public DayState? Day { get; set; }
    public NightState? Night { get; set; }
    public GameOverPayload? GameOver { get; set; }
}

public class SeatInfoResponse
{
    public Guid Id { get; set; }

    public string Nickname { get; set; } = "";

    public int SeatIndex { get; set; }
}

public class DayState
{
    public int DurationSeconds { get; set; }
    public int RemainingSeconds { get; set; }
    public ShowDayVotes ShowVoteTargets { get; set; }
    public List<SeatInfoResponse> AlivePlayers { get; set; } = new();
    public Dictionary<Guid, Guid?>? Votes { get; set; }
    public List<Guid> Enshrouded { get; set; } = new();
    public List<Guid> Asleep { get; set; } = new();
}

public class NightState
{
    public int DurationSeconds { get; set; }
    public int RemainingSeconds { get; set; }
    public List<PlayerNightInfoResponse> PlayerInfos { get; set; } = new();
    public List<SeatInfoResponse> AllAlivePlayers { get; set; } = new();
    public List<Guid> SubmittedPlayers { get; set; } = new();

    // Concrete submitted actions so clients can reconstruct per-player submission state
    public List<SubmittedActionResponse> SubmittedActions { get; set; } = new();
}

public class PlayerNightInfoResponse
{
    public Guid PlayerId { get; set; }
    public bool HasAbility { get; set; }
    public int MinTargets { get; set; }
    public int MaxTargets { get; set; }
    public List<Guid> ForbiddenTargets { get; set; } = new();
}

public class SubmittedActionResponse
{
    public Guid ActorId { get; set; }
    public ActionType ActionType { get; set; }
    public List<Guid> Targets { get; set; } = new();
}
