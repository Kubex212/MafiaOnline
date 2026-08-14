using Newtonsoft.Json;

namespace MafiaOnline.Engine.Night;

public enum ClueType
{
    Detective,
    DogTamer,
    Seer,
    Trailer,
    Peeper,
}

public abstract class NightEvent
{
}

public abstract class ClueEvent : NightEvent
{
    [JsonProperty]
    public Guid ClueOwnerId { get; private set; }

    [JsonProperty]
    public bool IsReversed { get; private set; }

    public abstract object DisplayedClue { get; }
    public abstract ClueType ClueType { get; }

    protected ClueEvent()
    {
    }

    protected ClueEvent(Guid clueOwnerId)
    {
        ClueOwnerId = clueOwnerId;
    }

    public void ReverseClue() => IsReversed = true;

    public virtual Dictionary<string, object> GetAdditionalInfoDictionary(IReadOnlyCollection<Player> players)
    {
        return new Dictionary<string, object>();
    }
}

public abstract class ClueEvent<T> : ClueEvent
{
    [JsonProperty]
    public T Clue { get; private set; }

    [JsonProperty]
    public T FakeClue { get; private set; }

    public override object DisplayedClue => IsReversed ? FakeClue! : Clue!;

    protected ClueEvent() // TODO: is this contructor needed for JSON deserialization?
    {
    }

    protected ClueEvent(Guid playerId, T clue, T fakeClue)
        : base(playerId)
    {
        Clue = clue;
        FakeClue = fakeClue;
    }
}

public sealed class DetectiveClueEvent : ClueEvent<string>
{
    public override ClueType ClueType => ClueType.Detective;

    public DetectiveClueEvent()
    {
    }

    public DetectiveClueEvent(Guid detectiveId, string nonWerewolf, string werewolf)
        : base(detectiveId, nonWerewolf, werewolf)
    {
    }
}

public sealed class DogTamerClueEvent : ClueEvent<bool>
{
    public override ClueType ClueType => ClueType.DogTamer;

    public DogTamerClueEvent()
    {
    }

    public DogTamerClueEvent(Guid dogTamerId, bool didBark)
        : base(dogTamerId, didBark, !didBark)
    {
    }
}

public sealed class SeerClueEvent : ClueEvent<bool>
{
    public override ClueType ClueType => ClueType.Seer;

    [JsonProperty]
    public Guid SubjectA { get; private set; }

    [JsonProperty]
    public Guid SubjectB { get; private set; }

    public SeerClueEvent()
    {
    }

    public SeerClueEvent(Guid seerId, Guid subjectA, Guid subjectB, bool sameTeam)
        : base(seerId, sameTeam, !sameTeam)
    {
        SubjectA = subjectA;
        SubjectB = subjectB;
    }

    public override Dictionary<string, object> GetAdditionalInfoDictionary(IReadOnlyCollection<Player> players)
    {
        var subjAName = players.FirstOrDefault(p => p.Id == SubjectA)?.Nickname ?? "?";
        var subjBName = players.FirstOrDefault(p => p.Id == SubjectB)?.Nickname ?? "?";

        return new Dictionary<string, object>
        {
            ["SubjectAName"] = subjAName,
            ["SubjectBName"] = subjBName,
            ["SubjectA"] = SubjectA,
            ["SubjectB"] = SubjectB
        };
    }
}

public sealed class TrailerClueEvent : ClueEvent<string>
{
    public override ClueType ClueType => ClueType.Trailer;

    public TrailerClueEvent()
    {
    }

    public TrailerClueEvent(Guid trailerId, string direction)
        : base(trailerId, direction, direction == "Left" ? "Right" : "Left")
    {
    }
}

public sealed class PeeperClueEvent : ClueEvent<bool>
{
    [JsonProperty]
    public Guid Subject { get; private set; }
    public override ClueType ClueType => ClueType.Peeper;

    public PeeperClueEvent()
    {
    }

    public PeeperClueEvent(Guid peeperId, Guid targetId, bool isVillager)
        : base(peeperId, isVillager, !isVillager)
    {
        Subject = targetId;
    }

    public override Dictionary<string, object> GetAdditionalInfoDictionary(IReadOnlyCollection<Player> players)
    {
        var subjName = players.FirstOrDefault(p => p.Id == Subject)?.Nickname ?? "?";

        return new Dictionary<string, object>
        {
            ["SubjectName"] = subjName,
            ["Subject"] = Subject
        };
    }
}

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



public sealed class PoacherTriggeredEvent : NightEvent
{
    public Guid PoacherId { get; }
    public Guid VictimId { get; }
    public Guid WerewolfId { get; }
    public PoacherTriggeredEvent(Guid poacherId, Guid victimId, Guid werewolfId) => (PoacherId, VictimId, WerewolfId) = (poacherId, victimId, werewolfId);
}

public sealed class DraculaEnshroudedEvent : NightEvent
{
    public Guid DraculaId { get; }
    public Guid TargetId { get; }
    public DraculaEnshroudedEvent(Guid draculaId, Guid targetId) => (DraculaId, TargetId) = (draculaId, targetId);
}

public sealed class DeceiverEvent : NightEvent
{
    public Guid DeceiverId { get; }
    public Guid TargetId { get; }
    public DeceiverEvent(Guid deceiverId, Guid targetId) => (DeceiverId, TargetId) = (deceiverId, targetId);
}

public sealed class CharmerSleepEvent : NightEvent
{
    public Guid CharmerId { get; }
    public Guid TargetId { get; }
    public CharmerSleepEvent(Guid charmerId, Guid targetId) => (CharmerId, TargetId) = (charmerId, targetId);
}