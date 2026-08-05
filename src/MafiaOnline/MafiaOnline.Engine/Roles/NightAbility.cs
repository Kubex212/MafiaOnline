using MafiaOnline.Engine.Night;
using System.Numerics;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Base class for all night abilities.
/// </summary>
/// <param name="Priority">
/// Determines the order in which night abilities fire (ascending).
/// Ties are broken by the acting player's seat index (ascending).
/// </param>
public abstract class NightAbility()
{
    /// <summary>
    /// When true, the ability is applied automatically without waiting for player input.
    /// When false, the game waits for the player to submit a <see cref="NightAction"/>
    /// before this ability's slot can be resolved.
    /// </summary>
    public abstract bool IsAutomatic { get; }

    /// <summary>
    /// Minimum number of targets this ability expects (for validation or UI hints).
    /// </summary>
    public virtual int MinTargets => 0;

    /// <summary>
    /// Maximum number of targets this ability accepts. Zero means no targets.
    /// Use int.MaxValue for unlimited.
    /// </summary>
    public virtual int MaxTargets => 1;

    /// <summary>
    /// Whether the ability can target the acting player.
    /// </summary>
    protected virtual bool CanTargetSelf => false;

    /// <summary>
    /// If the ability is used, how long should it be before it can be used again? Null means no cooldown.
    /// </summary>
    protected virtual int? Cooldown => null;

    /// <summary>
    /// When should the ability be available for the first time? Null means it is available from the start of the game.
    /// </summary>
    protected virtual int? NightsDelay => null;

    /// <summary>
    /// 
    /// </summary>
    protected virtual int TargetCooldown => 0;

    /// <summary>
    /// Execute the ability for <paramref name="actor"/> using the already-submitted
    /// <paramref name="action"/> (may be <c>null</c> for automatic abilities that
    /// choose their own target(s)).
    /// </summary>
    public abstract void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers);

    // Backwards-compatible helper: if an ability was implemented with the older
    // single-target signature, derived classes can override this instead.
    public virtual void Apply(Player actor, Guid? targetId, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        // Default behavior: wrap single target into a NightAction and call new Apply.
        NightAction? action = targetId is null ? null : new NightAction(actor.Id, new[] { targetId.Value }, ActionType.Ability);
        Apply(actor, action, context, alivePlayers);
    }

    public HashSet<Guid> GetForbiddenTargets(Player actor, IReadOnlyList<Player> players, IReadOnlyList<NightAction> previousActions)
    {
        var forbidden = new HashSet<Guid>();

        if (!CanTargetSelf)
            forbidden.Add(actor.Id);

        // Add any other game-specific restrictions here (e.g., cannot target dead players)
        foreach (var player in players)
        {
            if (!player.IsAlive)
                forbidden.Add(player.Id);

            
        }

        var previousActionsForTargetCooldown = previousActions.TakeLast(TargetCooldown);
        foreach (var action in previousActionsForTargetCooldown)
        {
            foreach (var target in action.Targets)
            {
                forbidden.Add(target);
            }
        }

        return forbidden;
    }
}
