namespace MafiaOnline.Engine.Config;

public class PoacherConfig
{
    /// <summary>
    /// Whether the poacher can place a trap on themselves.
    /// </summary>
    public bool AllowSelfTarget { get; set; } = false;

    /// <summary>
    /// Whether the poacher must alternate targets each night.
    /// </summary>
    public bool MustAlternate { get; set; } = true;
}
