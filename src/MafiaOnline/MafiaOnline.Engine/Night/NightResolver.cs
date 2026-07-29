using MafiaOnline.Engine.Roles;
using MafiaOnline.Engine.Config;

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
        IReadOnlyList<SubmittedNightAction> submittedActions,
        GameConfig? config = null)
    {
        var context = new NightContext();

        // Build list of ability slots. Include per-role abilities and a team-shared werewolf attack
        var slots = new List<(Player Player, NightAbility Ability)>();

        foreach (var player in alivePlayers.OrderBy(p => p.SeatIndex))
        {
            // Werewolf team: each member gets the shared attack ability slot
            if (player.Role.TeamId == 1)
            {
                slots.Add((player, new WerewolfAttackAbility()));
            }

            // Role-specific ability (if any)
            if (player.Role is IHasNightAbility na)
            {
                slots.Add((player, na.NightAbility));
            }
        }

        // Sort by ability priority ascending, ties broken by seat index
        var abilitySlots = slots
            .OrderBy(s => s.Player.SeatIndex)
            .ToList();

        foreach (var (player, ability) in abilitySlots)
        {
            NightAction? action = null;
            if (!ability.IsAutomatic)
            {
                var expectedActionType = ability is WerewolfAttackAbility ? ActionType.Attack : ActionType.Ability;
                action = submittedActions.SingleOrDefault(action => action.Owner == player.Id && action.Action.ActionType == expectedActionType)?.Action;
            }

            // Skip non-automatic abilities for which no action was submitted
            if (!ability.IsAutomatic && action is null) continue;

            ability.Apply(player, action, context, alivePlayers);
        }

        return BuildResult(context, alivePlayers, config);
    }


    private static NightResult BuildResult(NightContext context, IReadOnlyList<Player> alivePlayers, GameConfig? config)
    {
        var events = new List<NightEvent>();
        var killed = new List<Guid>();

        // Determine which targets are protected
        var protectedPlayerIds = new HashSet<Guid>(context.GuardianProtections.Values);

        // --- Resolve werewolf collective vote into at most one killed player ---
        if (context.WerewolfAttacks.Count > 0)
        {
            // Tally votes
            var tally = new Dictionary<Guid, int>();
            foreach (var target in context.WerewolfAttacks.Values)
            {
                tally.TryGetValue(target, out var cur);
                tally[target] = cur + 1;
            }

            // find top-voted targets
            var maxVotes = tally.Values.Max();
            var top = tally.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToList();

            var tieResolution = config?.Werewolf?.TieResolution ?? WerewolfTieResolution.Random;

            List<Guid> chosenTargets = new();

            if (top.Count == 1)
            {
                chosenTargets.Add(top[0]);
            }
            else
            {
                switch (tieResolution)
                {
                    case WerewolfTieResolution.NoKill:
                        // nobody dies
                        break;
                    case WerewolfTieResolution.All:
                        chosenTargets.AddRange(top);
                        break;
                    default: // Random
                        var rnd = new Random();
                        chosenTargets.Add(top[rnd.Next(top.Count)]);
                        break;
                }
            }

            foreach (var chosen in chosenTargets)
            {
                if (protectedPlayerIds.Contains(chosen))
                {
                    var guardianEntry = context.GuardianProtections.FirstOrDefault(kvp => kvp.Value == chosen);
                    events.Add(new AttackBlockedEvent(chosen, guardianEntry.Key));
                }
                else
                {
                    events.Add(new PlayerKilledEvent(chosen));
                    killed.Add(chosen);
                }
            }
        }

        var deceiverEvents = context.Events.OfType<DeceiverEvent>().ToList();
        var deceiverTargets = deceiverEvents.Select(e => e.TargetId).FirstOrDefault();
        var clueEvents = context.Events.OfType<ClueEvent>().ToList();

        // After initial kills, resolve poacher traps: if a trapped player was killed by werewolves,
        // one of the attacking werewolves (or any living werewolf) also dies.
        foreach (var (poacherId, trappedId) in context.PoacherTraps)
        {
            if (!killed.Contains(trappedId)) continue;

            // Find attacking werewolves who targeted this victim
            var attackers = context.WerewolfAttacks.Where(kvp => kvp.Value == trappedId).Select(kvp => kvp.Key).ToList();
            Guid? werewolfToDie = null;

            if (attackers.Count > 0)
            {
                // pick random attacker
                var rnd = new Random();
                werewolfToDie = attackers[rnd.Next(attackers.Count)];
            }
            else
            {
                // no direct attacker (edge case) — pick any alive werewolf
                var anyWerewolf = alivePlayers.FirstOrDefault(p => p.Role.TeamId == 1);
                if (anyWerewolf is not null) werewolfToDie = anyWerewolf.Id;
            }

            if (werewolfToDie.HasValue && !killed.Contains(werewolfToDie.Value))
            {
                killed.Add(werewolfToDie.Value);
                events.Add(new PoacherTriggeredEvent(poacherId, trappedId, werewolfToDie.Value));
            }
        }

        // Resolve detective clues — fire after kill resolution so subject's alive state is accurate
        foreach (var (detectiveId, subjectId) in context.DetectiveClues)
        {
            var subject = alivePlayers.FirstOrDefault(p => p.Id == subjectId);
            if (subject is null) continue;

            bool isWerewolf = subject.Role.TeamId != 0;
            events.Add(new DetectiveClueEvent(detectiveId, subject.Nickname, subject.Nickname)); // TODO: insert a werewolf
        }

        foreach(var (dogTamerId, shouldBark) in context.DogTamerClues)
        {
            events.Add(new DogTamerClueEvent(dogTamerId, shouldBark));
        }

        // Trailer clues
        foreach (var (trailerId, dir) in context.TrailerClues)
        {
            events.Add(new TrailerClueEvent(trailerId, dir));
        }

        // Enshrouded players
        foreach(var enshroudedId in context.Enshrouded)
        {
            events.Add(new DraculaEnshroudedEvent(enshroudedId, enshroudedId)); // TODO: add the Dracula's ID to the event
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
