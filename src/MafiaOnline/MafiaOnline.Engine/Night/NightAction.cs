namespace MafiaOnline.Engine.Night;

public sealed class NightAction
{
    public Guid Actor { get; }
    public IReadOnlyList<Guid> Targets { get; }
    public ActionType ActionType { get; }

    public NightAction(Guid actor, IEnumerable<Guid> targets, ActionType actionType)
    {
        Actor = actor;
        Targets = targets?.ToList() ?? new List<Guid>();
        ActionType = actionType;
    }
}

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
