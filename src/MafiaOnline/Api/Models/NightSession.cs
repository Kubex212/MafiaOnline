using MafiaOnline.Engine.Night;
using Newtonsoft.Json;

namespace MafiaOnline.Api.Models;

/// <summary>
/// Tracks the simultaneous night phase for one lobby.
/// All players act at the same time and must submit/skip their action.
/// </summary>
public class NightSession
{
    [JsonProperty]
    private HashSet<Guid> _playersPendingAction;

    [JsonProperty]
    private readonly List<NightAction> _submittedActions = new();

    [Newtonsoft.Json.JsonConstructor]
    public NightSession(HashSet<Guid>? playersNeedingAction, List<NightAction>? submittedActions, DateTime currentTurnStart)
    {
        _playersPendingAction = playersNeedingAction != null
            ? new (playersNeedingAction)
            : new ();
        if (submittedActions is not null)
        {
            foreach (var action in submittedActions)
                _submittedActions.Add(action);
        }
        SessionStartId = Guid.NewGuid();
        CurrentTurnStart = currentTurnStart;
    }

    public NightSession(IEnumerable<LobbyPlayer> alivePlayers)
    {
        // All alive players need to submit an action or skip
        _playersPendingAction = new HashSet<Guid>(alivePlayers.Select(p => p.Id));
        SessionStartId = Guid.NewGuid();
        CurrentTurnStart = DateTime.UtcNow;
    }

    public Guid SessionStartId { get; set; }
    public DateTime CurrentTurnStart { get; set; }
    public bool IsFinished => _playersPendingAction.Count == 0;
    public bool Resolved { get; set; }

    public IReadOnlySet<Guid> PlayersPendingAction => _playersPendingAction;
    public IReadOnlyList<NightAction> SubmittedActions => _submittedActions;

    /// <summary>
    /// Submit an action for a player. Only removes them from pending if they're truly done
    /// (based on their role requirements).
    /// </summary>
    public void SubmitAction(NightAction action)
    {
        if (!_playersPendingAction.Contains(action.Actor))
            throw new InvalidOperationException("This player has already submitted their action or does not need to act.");
        
        _submittedActions.Add(action);
        // Don't remove yet - let the service check if they're truly done
    }

    /// <summary>
    /// Mark a player as done with the night phase. Should only be called when the player
    /// has completed all their required actions (checked by the service).
    /// </summary>
    public void MarkPlayerDone(Guid playerId)
    {
        if (!_playersPendingAction.Contains(playerId))
            throw new InvalidOperationException("This player has already submitted their action or does not need to act.");
        
        _playersPendingAction.Remove(playerId);
    }

    /// <summary>Skip the player's turn (no action recorded) and mark them as done.</summary>
    public void Skip(Guid actorId)
    {
        if (!_playersPendingAction.Contains(actorId))
            throw new InvalidOperationException("This player has already submitted their action or does not need to act.");
        _playersPendingAction.Remove(actorId);
    }
}
