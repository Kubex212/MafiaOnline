using MafiaOnline.Engine.Config;

namespace MafiaOnline.Engine.Day;

/// <summary>
/// Pure resolver: takes a set of votes and config, returns who (if anyone) is banished.
/// </summary>
public class DayResolver
{
    public DayResult Resolve(
        IReadOnlyDictionary<Guid, Guid> votes,   // voter ? candidate
        IReadOnlyList<Guid> alivePlayers,
        DayConfig config,
        HashSet<Guid> enshroudedPlayers)
    {
        AssertNoVotesOnEnshroudedPlayer(votes, enshroudedPlayers);

        var context = new DayContext()
        {
            EnshroudedPlayers = enshroudedPlayers.ToHashSet()
        };

        // Tally
        var tally = new Dictionary<Guid, int>();
        foreach (var (_, candidate) in votes)
        {
            tally.TryGetValue(candidate, out int current);
            tally[candidate] = current + 1;
        }

        if (tally.Count == 0)
            return new DayResult { VoteTally = tally };

        int requiredVotes = (int)Math.Ceiling(alivePlayers.Count * config.VotesRequiredFraction);
        int maxVotes = tally.Values.Max();

        bool thresholdMet = maxVotes >= requiredVotes;

        if (!thresholdMet)
            return new DayResult { VoteTally = tally, ThresholdMet = false };

        var topCandidates = tally
            .Where(kvp => kvp.Value == maxVotes)
            .Select(kvp => kvp.Key)
            .ToList();

        List<Guid> banished = config.DrawResolution switch
        {
            DrawResolution.KickNone  => topCandidates.Count == 1 ? topCandidates : [],
            DrawResolution.KickAll   => topCandidates,
            DrawResolution.KickRandom => [topCandidates[new Random().Next(topCandidates.Count)]],
            _ => []
        };

        return new DayResult
        {
            Banished = banished,
            VoteTally = tally,
            ThresholdMet = thresholdMet
        };
    }

    private void AssertNoVotesOnEnshroudedPlayer(IReadOnlyDictionary<Guid, Guid> votes, HashSet<Guid> enshroudedPlayers)
    {
        foreach(var (_, candidate) in votes)
        {
            if (enshroudedPlayers.Contains(candidate))
                throw new InvalidOperationException($"Vote cast for enshrouded player {candidate}");
        }
    }
}
