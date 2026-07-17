namespace MafiaOnline.Engine.Night;

/// <summary>
/// Immutable record of a single thing that happened during the night.
/// </summary>
public abstract record NightEvent;

/// <summary>A player was killed by werewolves.</summary>
public record PlayerKilledEvent(Guid VictimId) : NightEvent;

/// <summary>An attack on a player was blocked by the Guardian.</summary>
public record AttackBlockedEvent(Guid VictimId, Guid GuardianId) : NightEvent;

/// <summary>
/// The Detective received a clue about a player's innocence/alignment.
/// </summary>
public record DetectiveClueEvent(Guid DetectiveId, Guid SubjectId, bool IsWerewolf) : NightEvent;
