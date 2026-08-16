using Api.Dtos;
using MafiaOnline.Engine.Config;
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
    public string? ActivePlayerId { get; set; }
    public int ActiveSeatIndex { get; set; }
    public bool HasAbility { get; set; }
    public List<SeatInfoResponse> AlivePlayers { get; set; } = new();
    public int RemainingSeconds { get; set; }
    public Dictionary<Guid, Guid>? SubmittedActions { get; set; }
}
