namespace MafiaOnline.Engine.Config;

/// <summary>
/// Toggles for optional special roles. Standard Werewolf and Villager are always present.
/// </summary>
public class RoleSelectionConfig
{
    public bool Guardian { get; set; } = true;
    public bool Detective { get; set; } = true;
    public bool DogTamer { get; set; } = true;
    public bool Poacher { get; set; } = true;
    public bool Trailer { get; set; } = true;
    public bool Seer { get; set; } = false;
    public bool Fool { get; set; } = true;
    public bool Dracula { get; set; } = true;
}
