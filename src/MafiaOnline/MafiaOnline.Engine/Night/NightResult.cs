namespace MafiaOnline.Engine.Night;

/// <summary>
/// The resolved outcome of a complete night phase.
/// </summary>
public class NightResult
{
    public IReadOnlyList<NightEvent> Events { get; init; } = [];

    /// <summary>Ids of players who were killed and must be eliminated from the game.</summary>
    public IReadOnlyList<Guid> Killed { get; init; } = [];
}
