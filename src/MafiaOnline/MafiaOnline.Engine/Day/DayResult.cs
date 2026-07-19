namespace MafiaOnline.Engine.Day;

/// <summary>Immutable result produced after day vote resolution.</summary>
public class DayResult
{
    /// <summary>Player IDs that were banished (0–many depending on draw resolution).</summary>
    public IReadOnlyList<Guid> Banished { get; init; } = [];

    /// <summary>Detailed vote tally: candidate ID ? vote count.</summary>
    public IReadOnlyDictionary<Guid, int> VoteTally { get; init; } = new Dictionary<Guid, int>();

    /// <summary>Whether the top vote count met the required threshold.</summary>
    public bool ThresholdMet { get; init; }
}
