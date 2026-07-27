namespace MafiaOnline.Engine.Night;

public sealed class NightAction
{
    public IReadOnlyList<Guid> Targets { get; }
    public ActionType ActionType { get; }

    public NightAction(IEnumerable<Guid> targets, ActionType actionType)
    {
        Targets = targets?.ToList() ?? new List<Guid>();
        ActionType = actionType;
    }
}

public record SubmittedNightAction(Guid Owner, NightAction Action);

public class ActionData
{
    public ActionType Type { get; set; }
    public IReadOnlyList<Guid> Targets { get; set; } = new List<Guid>();
    public IReadOnlyList<Guid> Attack { get; set; } = new List<Guid>();
}

public enum ActionType
{
    Attack,
    Ability,
    Other
}
