namespace MafiaOnline.Engine.Config;

/// <summary>
/// Configuration options for the Detective role.
/// </summary>
public class DogTamerConfig
{
    /// <summary>
    /// Number of neighboring players to investigate. The Dog Tamer will investigate the player and their neighbors, up to this range (range is per direction).
    /// </summary>
    public int Range { get; set; } = 2;

    /// <summary>
    /// Number of werewolves in range in order for his dog to bark. If there are fewer or more than this number of werewolves in range, the dog will not bark.
    /// </summary>
    public int NumWerewolves { get; set; } = 1;
}
