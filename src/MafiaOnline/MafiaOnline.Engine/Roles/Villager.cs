namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Ordinary villager — no night ability, just votes during the day.
/// </summary>
public class Villager : IRole
{
    public RoleType Type => RoleType.Villager;
    public string Name => "Villager";
    public Team Team => Team.Villagers;
}