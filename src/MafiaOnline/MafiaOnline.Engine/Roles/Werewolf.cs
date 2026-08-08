namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Werewolf — member of werewolf team. Team-shared kill vote lives in NightResolver logic.
/// </summary>
public class Werewolf : IRole
{
    public RoleType Type => RoleType.Werewolf;
    public string Name => "Werewolf";
    public Team Team => Team.Werewolves;
}