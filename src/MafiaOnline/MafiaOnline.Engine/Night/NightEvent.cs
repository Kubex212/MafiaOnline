namespace MafiaOnline.Engine.Night;

/// <summary>
/// Immutable record of a single thing that happened during the night.
/// </summary>
public abstract record NightEvent;

/// <summary>
/// Events that are specific to a player and can be reported to them.
/// </summary>
/// <param name="PlayerId"></param>
public abstract record PlayerNightEvent(Guid PlayerId) : NightEvent
{
    public abstract PlayerNightInfo GetInfo();
}


/// <summary>
/// A player was killed by werewolves.
/// </summary>
public record PlayerKilledEvent(Guid VictimId) : NightEvent;

/// <summary>
/// An attack on a player was blocked by the Guardian.
/// </summary>
public record AttackBlockedEvent(Guid VictimId, Guid GuardianId) : NightEvent;

/// <summary>
/// The Detective received a clue about a player's innocence/alignment.
/// </summary>
public record DetectiveClueEvent(Guid PlayerId, Guid SubjectId, bool IsWerewolf) : PlayerNightEvent(PlayerId)
{
    public override PlayerNightInfo GetInfo()
    {
        return new PlayerNightInfo(
            "DetectiveClue",
            new Dictionary<string, object>
            {
                ["SubjectId"] = SubjectId,
                ["IsWerewolf"] = IsWerewolf
            });
    }
}

/// <summary>
/// Dog Tamer received a clue about whether their dog barked or not.
/// </summary>
/// <param name="DogTamerId"></param>
/// <param name="DidBark"></param>
public record DogTamerClueEvent(Guid PlayerId, bool DidBark) : PlayerNightEvent(PlayerId)
{
    public override PlayerNightInfo GetInfo()
    {
        return new PlayerNightInfo(
            "DogTamerClue",
            new Dictionary<string, object>
            {
                ["DidBark"] = DidBark
            });
    }
}
