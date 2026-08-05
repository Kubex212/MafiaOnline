using MafiaOnline.Engine.Config;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Guardian (citizen) — each night chooses one player to protect from werewolf attack.
/// Configurable: can optionally protect themselves and/or the same player consecutively.
/// </summary>
public class Guardian : IRole, IHasNightAbility
{
    private readonly GuardianConfig _config;

    public Guardian(GuardianConfig? config = null)
    {
        _config = config ?? new GuardianConfig();
        NightAbility = new GuardianProtectAbility(_config);
    }

    public RoleType Type => RoleType.Guardian;
    public Team Team => Team.Villagers;
    public string Name => "Guardian";
    public int TeamId => 0;

    public NightAbility NightAbility { get; }
}