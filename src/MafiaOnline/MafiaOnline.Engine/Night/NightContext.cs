namespace MafiaOnline.Engine.Night;

/// <summary>
/// A mutable bag that night abilities read from and write to during resolution.
/// Abilities are applied in priority order (ascending); each ability can inspect
/// what earlier abilities already registered and conditionally add its own effects.
/// </summary>
public class NightContext
{
    // ---------- pending decisions (set by abilities) ----------

    /// <summary>PlayerId ? targeted victim (by Werewolves).</summary>
    internal Dictionary<Guid, Guid> WerewolfAttacks { get; } = new();

    /// <summary>PlayerId (guardian) ? protected target.</summary>
    internal Dictionary<Guid, Guid> GuardianProtections { get; } = new();

    /// <summary>PlayerId (detective) ? the player pointed at them this night.</summary>
    internal Dictionary<Guid, Guid> DetectiveClues { get; } = new();

    // ---------- finalised outcomes (populated by NightResolver) ----------

    internal List<NightEvent> Events { get; } = new();
}
