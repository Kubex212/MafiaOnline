namespace MafiaOnline.Engine.Config;

/// <summary>
/// Configuration options for the Detective role.
/// </summary>
public class DetectiveConfig
{
    /// <summary>
    /// Whether the Detective can be pointed at a Werewolf and have that revealed.
    /// When false, the detective only gets confirmed-innocent clues.
    /// Default: true (standard Mafia rules — detective gets correct alignment info).
    /// </summary>
    public bool CanRevealWerewolves { get; set; } = true;
}
