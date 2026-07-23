using MafiaOnline.Engine.Roles;

namespace MafiaOnline.Engine.Night;

/// <summary>
/// Resolves the night phase given a set of submitted player actions.
/// </summary>
public class NightResolver
{
    /// <summary>
    /// Execute all night abilities and produce a <see cref="NightResult"/>.
    /// </summary>
    /// <param name="alivePlayers">Players still alive at the start of the night, in seat order.</param>
    /// <param name="submittedActions">
    /// Actions submitted by players who have non-automatic abilities.
    /// Key = acting player id, Value = chosen NightAction.
    /// </param>
    public NightResult Resolve(
        IReadOnlyList<Player> alivePlayers,
        IReadOnlyDictionary<Guid, NightAction> submittedActions)
    {
        var context = new NightContext();

        // Build ordered list of (player, ability) pairs
        var abilitySlots = alivePlayers
            .Where(p => p.Role is IHasNightAbility)
            .Select(p => (Player: p, Ability: ((IHasNightAbility)p.Role).NightAbility))
            .OrderBy(s => s.Player.SeatIndex)
            .ToList();

        foreach (var (player, ability) in abilitySlots)
        {
            NightAction? action = null;
            if (!ability.IsAutomatic)
            {
                if (submittedActions.TryGetValue(player.Id, out var a)) action = a;
            }

            // Skip non-automatic abilities for which no action was submitted
            if (!ability.IsAutomatic && action is null) continue;

            ability.Apply(player, action, context, alivePlayers);
        }

        return BuildResult(context, alivePlayers);
    }

    // Legacy adapter for existing code passing Guid->Guid mapping
    public NightResult Resolve(
        IReadOnlyList<Player> alivePlayers,
        IReadOnlyDictionary<Guid, Guid> submittedActions)
    {
        var adapted = submittedActions.ToDictionary(kvp => kvp.Key, kvp => new NightAction(new[] { kvp.Value } as IEnumerable<Guid>));
        return Resolve(alivePlayers, adapted);
    }

    private static NightResult BuildResult(NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        var events = new List<NightEvent>();
        var killed = new List<Guid>();

        // Determine which targets are protected
        var protectedPlayerIds = new HashSet<Guid>(context.GuardianProtections.Values);

        // Resolve attacks — each attacked player either dies or is saved
        foreach (var (guardianId, victimId) in context.WerewolfAttacks)
        {
            if (protectedPlayerIds.Contains(victimId))
            {
                // Find which guardian protected this victim
                var guardianEntry = context.GuardianProtections
                    .FirstOrDefault(kvp => kvp.Value == victimId);

                events.Add(new AttackBlockedEvent(victimId, guardianEntry.Key));
            }
            else
            {
                events.Add(new PlayerKilledEvent(victimId));
                killed.Add(victimId);
            }
        }

        // Resolve detective clues — fire after kill resolution so subject's alive state is accurate
        foreach (var (detectiveId, subjectId) in context.DetectiveClues)
        {
            var subject = alivePlayers.FirstOrDefault(p => p.Id == subjectId);
            if (subject is null) continue;

            bool isWerewolf = subject.Role.TeamId != 0;
            events.Add(new DetectiveClueEvent(detectiveId, subject.Nickname, isWerewolf));
        }

        foreach(var (dogTamerId, shouldBark) in context.DogTamerClues)
        {
            events.Add(new DogTamerClueEvent(dogTamerId, shouldBark));
        }

        // Include any events directly registered in context
        events.AddRange(context.Events);

        return new NightResult
        {
            Events = events,
            Killed = killed
        };
    }
}
