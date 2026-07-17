using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Base class for all night abilities.
/// </summary>
/// <param name="Priority">
/// Determines the order in which night abilities fire (ascending).
/// Ties are broken by the acting player's seat index (ascending).
/// </param>
public abstract class NightAbility(int priority)
{
    public int Priority { get; } = priority;

    /// <summary>
    /// When true, the ability is applied automatically without waiting for player input.
    /// When false, the game waits for the player to submit a <see cref="NightActionCommand"/>
    /// before this ability's slot can be resolved.
    /// </summary>
    public abstract bool IsAutomatic { get; }

    /// <summary>
    /// Execute the ability for <paramref name="actor"/> using the already-submitted
    /// <paramref name="targetId"/> (may be <c>null</c> for automatic abilities that
    /// choose their own target).
    /// </summary>
    public abstract void Apply(Player actor, Guid? targetId, NightContext context, IReadOnlyList<Player> alivePlayers);
}
