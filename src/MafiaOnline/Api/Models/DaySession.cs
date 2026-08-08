namespace MafiaOnline.Api.Models;

/// <summary>
/// Tracks votes cast during the day phase for one lobby.
/// All alive players may vote (or abstain); the session closes either when
/// everybody has voted or the timer fires.
/// </summary>
public class DaySession
{
    [Newtonsoft.Json.JsonProperty]
    private HashSet<Guid> _alivePlayers;

    [Newtonsoft.Json.JsonProperty]
    private Dictionary<Guid, Guid?> _votes = new();  // voter ? candidate (nullable => skip)

    [Newtonsoft.Json.JsonConstructor]
    public DaySession()
    {
        _alivePlayers = new HashSet<Guid>();
        _votes = new Dictionary<Guid, Guid?>();
    }

    public DaySession(IEnumerable<LobbyPlayer> alivePlayers)
    {
        _alivePlayers = alivePlayers.Select(p => p.Id).ToHashSet();
    }

    public IReadOnlyDictionary<Guid, Guid?> Votes => _votes;

    public bool AllVoted => _alivePlayers.All(id => _votes.ContainsKey(id));

    public bool Resolved { get; set; }

    /// <summary>Cast or change a vote. Idempotent – the last submitted vote wins.</summary>
    public void CastVote(Guid voterId, Guid? candidateId)
    {
        if (!_alivePlayers.Contains(voterId))
            throw new InvalidOperationException("Voter is not an alive player.");

        if (candidateId.HasValue && !_alivePlayers.Contains(candidateId.Value))
            throw new InvalidOperationException("Candidate is not an alive player.");

        if (candidateId.HasValue && voterId == candidateId.Value)
            throw new InvalidOperationException("Players cannot vote for themselves.");

        _votes[voterId] = candidateId;
    }

    /// <summary>Returns whether a specific player has already voted.</summary>
    public bool HasVoted(Guid voterId) => _votes.ContainsKey(voterId);
}
