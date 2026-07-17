namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Werewolf — each night chooses a town player to kill.
/// </summary>
public class Werewolf : IRole, IHasNightAbility
{
    public RoleType Type => RoleType.Werewolf;
    public string Name => "Werewolf";
    public int TeamId => 1;

    public NightAbility NightAbility { get; } = new WerewolfAttackAbility();
}