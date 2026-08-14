using MafiaOnline.Engine.Roles;

namespace MafiaOnline.Engine.Night;

/// <summary>
/// A mutable bag that night abilities read from and write to during resolution.
/// Abilities are applied in priority order (ascending); each ability can inspect
/// what earlier abilities already registered and conditionally add its own effects.
/// </summary>
public class NightContext
{
    internal Player? DrunkPlayer { get; set; } = null;
    // ---------- pending decisions (set by abilities) ----------

    /// <summary>
    /// PlayerId -> targeted victim (by Werewolves).
    /// </summary>
    internal Dictionary<Guid, Guid> WerewolfAttacks { get; } = new();

    /// <summary>
    /// PlayerId (guardian) -> protected target.
    /// </summary>
    internal Dictionary<Guid, Guid> GuardianProtections { get; } = new();

    /// <summary>
    /// PlayerId (detective) -> the player pointed at them this night.
    /// </summary>
    internal DetectiveClue? DetectiveClue { get; set; } = null;

    /// <summary>
    /// PlayerId (dog tamer) -> whether the dog barked.
    /// </summary>
    internal Dictionary<Guid, bool> DogTamerClues { get; } = new();

    /// <summary>
    /// PlayerId (poacher) -> trapped target this night.
    /// </summary>
    internal Dictionary<Guid, Guid> PoacherTraps { get; } = new();

    /// <summary>
    /// PlayerId (trailer) -> direction clue ("Left"/"Right").
    /// </summary>
    internal Dictionary<Guid, string> TrailerClues { get; } = new();

    /// <summary>
    /// Enshrouded players (Dracula ability) — set of player IDs that cannot be voted out during the next day. For now there should be only 1 enshrouded player at once.
    /// </summary>
    internal HashSet<Guid> Enshrouded { get; } = new();

    /// <summary>
    /// Peeper self-kills — set of peeper IDs that were killed due to peeping on a werewolf. For now there should be only 1 peeper kill at once.
    /// </summary>
    internal HashSet<Guid> PeeperKills { get; } = new();

    /// <summary>
    /// Players put to sleep by Charmer. For now there should be only 1 sleeping player at once unless configured otherwise.
    /// </summary>
    internal HashSet<Guid> SleepingPlayers { get; } = new();

    // ---------- finalised outcomes (populated by NightResolver) ----------

    internal List<NightEvent> Events { get; } = new();

    /// <summary>
    /// Add a custom NightEvent produced by an ability.
    /// </summary>
    public void AddEvent(NightEvent ev) => Events.Add(ev);

}
