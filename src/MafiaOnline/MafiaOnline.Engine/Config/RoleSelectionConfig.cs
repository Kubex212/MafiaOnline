namespace MafiaOnline.Engine.Config;

/// <summary>
/// Toggles for optional special roles. Standard Werewolf and Villager are always present and are used only to fill any missing assignments (e.g. if there are 10 players but only 8 roles were selected).
/// </summary>
public class RoleSelectionConfig
{
    public bool Guardian { get; set; } = true;
    public bool Detective { get; set; } = true;
    public bool DogTamer { get; set; } = true;
    public bool Poacher { get; set; } = true;
    public bool Trailer { get; set; } = true;
    public bool Seer { get; set; } = true;
    public bool Peeper { get; set; } = true;
    public bool Fool { get; set; } = true;
    public bool Dracula { get; set; } = true;
    public bool Deceiver { get; set; } = true;
    public bool Ghost { get; set; } = true;
    public bool Charmer { get; set; } = true;
}
