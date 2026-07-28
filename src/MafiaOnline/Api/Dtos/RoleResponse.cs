namespace MafiaOnline.Api.Dtos;

/// <summary>
/// Contains information about a player's role and any information that they should be granted at the start of the game.
/// </summary>
public class RoleResponse
{
    public string Name { get; set; } = "";

    public string TeamName { get; set; } = "";
    public int TeamId { get; set; }
    public string? Alibi { get; set; }
}
