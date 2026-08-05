using MafiaOnline.Engine.Night;
using Newtonsoft.Json;

namespace MafiaOnline.Api.Models;

/// <summary>
/// Tracks the turn-by-turn night phase for one lobby.
/// Players act in seat order; non-ability players must still press Skip (to hide role information).
/// This session supports multiple sub-actions per player (e.g. werewolf team attack + role ability).
/// </summary>
public class NightSession
{
    [Newtonsoft.Json.JsonProperty]
    private List<LobbyPlayer> _turnOrder;

    [JsonProperty]
    private readonly List<NightAction> _submittedActions = new();

    [Newtonsoft.Json.JsonConstructor]
    public NightSession(List<LobbyPlayer> turnOrder, List<NightAction>? submittedActions, int currentTurnIndex)
    {
        _turnOrder = turnOrder ?? new List<LobbyPlayer>();
        if (submittedActions is not null)
        {
            foreach (var action in submittedActions)
                _submittedActions.Add(action);
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

    public IReadOnlyList<NightAction> SubmittedActions => _submittedActions;

    /// <summary>Record a full action for the current player and advance the turn (legacy).</summary>
    public void SubmitAction(NightAction action)
    {
        AssertIsActivePlayer(action.Actor);
        _submittedActions.Add(action);
    }

    /// <summary>Skip the current player's turn (no action recorded), then advance.</summary>
    public void Skip(Guid actorId)
    {
        AssertIsActivePlayer(actorId);
        Advance();
    }

    /// <summary>Force-advance the current turn (timeout).</summary>
    public void ForceAdvance() => Advance();

    public void Advance() => CurrentTurnIndex++;

    private void AssertIsActivePlayer(Guid actorId)
    {
        if (ActivePlayer?.Id != actorId)
            throw new InvalidOperationException("It is not this player's turn.");
    }
}
