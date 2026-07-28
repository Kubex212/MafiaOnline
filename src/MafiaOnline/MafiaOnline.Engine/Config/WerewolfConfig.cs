namespace MafiaOnline.Engine.Config;

public class WerewolfConfig
{
    // When true, werewolf vote targets are visible to other werewolves
    public bool ShowKillVotesToTeam { get; set; } = true;

    // How to resolve ties for werewolf kill votes
    // "NoKill" means nobody dies on tie, "Random" picks one of top tied targets, "All" kills all top tied
    public WerewolfTieResolution TieResolution { get; set; } = WerewolfTieResolution.Random;

    public bool ShowAlibi { get; set; } = true;
}

public enum WerewolfTieResolution
{
    NoKill,
    Random,
    All
}