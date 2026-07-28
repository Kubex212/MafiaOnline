namespace MafiaOnline.Engine.Config;

public enum DrawResolution
{
    /// <summary>Nobody is banished on a tie.</summary>
    KickNone,
    /// <summary>One of the tied players is chosen at random.</summary>
    KickRandom,
    /// <summary>All tied players are banished.</summary>
    KickAll
}

public enum ShowDayVotes
{
    Never,
    AfterVotingEnds,
    Always
}

/// <summary>Configuration for the day discussion + voting phase.</summary>
public class DayConfig
{
    /// <summary>Total duration of the day phase in seconds. Default: 180.</summary>
    public int DurationSeconds { get; set; } = 180;

    /// <summary>
    /// Minimum fraction of alive players whose votes a candidate must receive to be banished.
    /// Uses ceiling, so with 4 players and 0.5 the threshold is 2 votes.
    /// Default: 0.5.
    /// </summary>
    public double VotesRequiredFraction { get; set; } = 0.5;

    /// <summary>How to handle a tie between the highest-voted candidates.</summary>
    public DrawResolution DrawResolution { get; set; } = DrawResolution.KickNone;

    /// <summary>
    /// When true, every client is told in real time who each voter is targeting.
    /// When false, only the fact that a player has voted (not their target) is broadcast.
    /// </summary>
    public ShowDayVotes ShowVoteTargets { get; set; } = ShowDayVotes.AfterVotingEnds;
}
