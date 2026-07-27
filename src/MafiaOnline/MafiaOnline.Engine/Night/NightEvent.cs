namespace MafiaOnline.Engine.Night;

public abstract class NightEvent { }

public sealed class PlayerKilledEvent : NightEvent
{
    public Guid PlayerId { get; }
    public PlayerKilledEvent(Guid playerId) => PlayerId = playerId;
}

public sealed class AttackBlockedEvent : NightEvent
{
    public Guid VictimId { get; }
    public Guid GuardianId { get; }
    public AttackBlockedEvent(Guid victimId, Guid guardianId) => (VictimId, GuardianId) = (victimId, guardianId);
}

public sealed class DetectiveClueEvent : NightEvent
{
    public Guid DetectiveId { get; }
    public string SubjectNickname { get; }
    public bool IsWerewolf { get; }
    public DetectiveClueEvent(Guid detectiveId, string subjectNickname, bool isWerewolf) => (DetectiveId, SubjectNickname, IsWerewolf) = (detectiveId, subjectNickname, isWerewolf);
}

public sealed class DogTamerClueEvent : NightEvent
{
    public Guid DogTamerId { get; }
    public bool DidBark { get; }
    public DogTamerClueEvent(Guid dogTamerId, bool didBark) => (DogTamerId, DidBark) = (dogTamerId, didBark);
}

public sealed class SeerClueEvent : NightEvent
{
    public Guid SeerId { get; }
    public Guid SubjectA { get; }
    public Guid SubjectB { get; }
    public bool SameTeam { get; }
    public SeerClueEvent(Guid seerId, Guid subjectA, Guid subjectB, bool sameTeam) => (SeerId, SubjectA, SubjectB, SameTeam) = (seerId, subjectA, subjectB, sameTeam);
}

public sealed class PoacherTriggeredEvent : NightEvent
{
    public Guid PoacherId { get; }
    public Guid VictimId { get; }
    public Guid WerewolfId { get; }
    public PoacherTriggeredEvent(Guid poacherId, Guid victimId, Guid werewolfId) => (PoacherId, VictimId, WerewolfId) = (poacherId, victimId, werewolfId);
}

public sealed class TrailerClueEvent : NightEvent
{
    public Guid TrailerId { get; }
    public string Direction { get; }
    public TrailerClueEvent(Guid trailerId, string direction) => (TrailerId, Direction) = (trailerId, direction);
}

public sealed class DraculaEnshroudedEvent : NightEvent
{
    public Guid DraculaId { get; }
    public Guid TargetId { get; }
    public DraculaEnshroudedEvent(Guid draculaId, Guid targetId) => (DraculaId, TargetId) = (draculaId, targetId);
}