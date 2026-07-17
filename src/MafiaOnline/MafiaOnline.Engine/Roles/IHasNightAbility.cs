using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Marks a role as having a night-phase ability.
/// </summary>
public interface IHasNightAbility
{
    NightAbility NightAbility { get; }
}
