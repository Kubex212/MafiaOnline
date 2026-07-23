namespace MafiaOnline.Engine.Night;

public sealed class NightAction
{
    public IReadOnlyList<Guid> Targets { get; }
    public IReadOnlyDictionary<string, object> Data { get; }

    public NightAction(IEnumerable<Guid> targets, IReadOnlyDictionary<string, object>? data = null)
    {
        Targets = targets?.ToList() ?? new List<Guid>();
        Data = data ?? new Dictionary<string, object>();
    }
}
