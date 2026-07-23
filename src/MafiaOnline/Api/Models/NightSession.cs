namespace MafiaOnline.Api.Models;

/// <summary>
/// Tracks the turn-by-turn night phase for one lobby.
/// Players act in seat order; non-ability players must still press Skip (to hide role information).
/// </summary>
public class NightSession
{
    [Newtonsoft.Json.JsonProperty]
    private List<LobbyPlayer> _turnOrder;

    [Newtonsoft.Json.JsonProperty]
    private readonly Dictionary<Guid, Engine.Night.NightAction> _submittedActions = new();

    [Newtonsoft.Json.JsonConstructor]
    public NightSession(List<LobbyPlayer> turnOrder, Dictionary<Guid, Engine.Night.NightAction>? submittedActions, int currentTurnIndex)
    {
        _turnOrder = turnOrder ?? new List<LobbyPlayer>();
        if (submittedActions is not null)
        {
            foreach (var kv in submittedActions)
                _submittedActions[kv.Key] = kv.Value;
        }

        CurrentTurnIndex = currentTurnIndex;
    }

    public NightSession(IEnumerable<LobbyPlayer> alivePlayers)
    {
        // Turn order = alive players sorted by seat index (clockwise from seat 0)
        _turnOrder = alivePlayers.OrderBy(p => p.SeatIndex).ToList();
        CurrentTurnIndex = 0;
    }

    public int CurrentTurnIndex { get; private set; }

    public bool IsFinished => CurrentTurnIndex >= _turnOrder.Count;

    public bool Resolved { get; set; }

    public LobbyPlayer? ActivePlayer => IsFinished ? null : _turnOrder[CurrentTurnIndex];

    public IReadOnlyDictionary<Guid, Engine.Night.NightAction> SubmittedActions => _submittedActions;

    /// <summary>Record a targeted action for the current player, then advance the turn.</summary>
    public void SubmitAction(Guid actorId, Engine.Night.NightAction action)
    {
        AssertIsActivePlayer(actorId);
        _submittedActions[actorId] = action;
        Advance();
    }

    /// <summary>Legacy single-target overload kept for compatibility.</summary>
    public void SubmitAction(Guid actorId, Guid targetId)
    {
        SubmitAction(actorId, new Engine.Night.NightAction(new[] { targetId }));
    }

    /// <summary>Skip the current player's turn (no action recorded), then advance.</summary>
    public void Skip(Guid actorId)
    {
        AssertIsActivePlayer(actorId);
        Advance();
    }

    /// <summary>Force-advance the current turn (timeout).</summary>
    public void ForceAdvance() => Advance();

    private void Advance() => CurrentTurnIndex++;

    private void AssertIsActivePlayer(Guid actorId)
    {
        if (ActivePlayer?.Id != actorId)
            throw new InvalidOperationException("It is not this player's turn.");
    }
}
