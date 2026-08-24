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
    /// <param name="allPlayers">Players still alive at the start of the night, in seat order.</param>
    /// <param name="submittedActions">
    /// Actions submitted by players who have non-automatic abilities.
    /// Key = acting player id, Value = chosen NightAction.
    /// </param>
    public NightResult Resolve(
        IReadOnlyList<Player> allPlayers,
        IReadOnlyList<NightAction> submittedActions,
        Game game,
        GameConfig config = null)
    {
        if (config is null)
            config = GameConfig.GetDefault();

        var context = new NightContext(game);

        // Build list of ability slots. Include per-role abilities and a team-shared werewolf attack
        var slots = new List<(Player Player, NightAbility Ability)>();

        foreach (var player in allPlayers.OrderBy(p => p.SeatIndex))
        {
            // Werewolf team: each member gets the shared attack ability slot
            if (player.Role.Team == Team.Werewolves)
            {
                slots.Add((player, new WerewolfAttackAbility()));
            }

            // Role-specific ability (if any)
            if (player.Role is IHasNightAbility na)
            {
                slots.Add((player, na.NightAbility));
            }
        }

        var abilitySlots = slots
            .OrderBy(s => s.Ability is CharmerSleepAbility ? 0 : 1)
            .ToList();

        foreach (var (player, ability) in abilitySlots)
        {
            NightAction? action = null;
            if (!ability.IsAutomatic)
            {
                var expectedActionType = ability is WerewolfAttackAbility ? ActionType.Attack : ActionType.Ability;
                action = submittedActions.SingleOrDefault(action => action.Actor == player.Id && action.ActionType == expectedActionType);
            }

            // Skip non-automatic abilities for which no action was submitted
            if (!ability.IsAutomatic && action is null) continue;

            // sleeping players cannot use abilities
            if (context.SleepingPlayers.Contains(player.Id)) continue;

            ability.Apply(player, action, context, allPlayers);
        }

        context.DrunkPlayer = game.DrunkPlayer;

        return BuildResult(context, allPlayers, config, submittedActions);
    }


    private static NightResult BuildResult(NightContext context, IReadOnlyList<Player> allPlayers, GameConfig config, IReadOnlyList<NightAction> actions)
    {
        var events = new List<NightEvent>();
        var killed = new List<Guid>();
        var alivePlayers = allPlayers.Where(p => p.IsAlive);

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
                    if (config?.Guardian?.AnnounceProtection == true)
                    {
                        var guardianEntry = context.GuardianProtections.FirstOrDefault(kvp => kvp.Value == chosen);
                        events.Add(new AttackBlockedEvent(chosen, guardianEntry.Key));
                    }
                }
                else
                {
                    events.Add(new PlayerKilledEvent(chosen));
                    killed.Add(chosen);
                }
            }
        }

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
                var anyWerewolf = alivePlayers.FirstOrDefault(p => p.Role.Team == Team.Werewolves);
                if (anyWerewolf is not null) werewolfToDie = anyWerewolf.Id;
            }

            if (werewolfToDie.HasValue && !killed.Contains(werewolfToDie.Value))
            {
                killed.Add(werewolfToDie.Value);
                events.Add(new PoacherTriggeredEvent(poacherId, trappedId, werewolfToDie.Value));
            }
        }

        // Resolve detective clue — fire after kill resolution so subject's alive state is accurate
        if (context.DetectiveClue is not null)
        {
            var clue = context.DetectiveClue;
            var subject = alivePlayers.FirstOrDefault(p => p.Id == clue.VillagerId)!;
            var werewolf = alivePlayers.FirstOrDefault(p => p.Id == clue.WerewolfId)!;

            events.Add(new DetectiveClueEvent(clue.DetectiveId, subject.Nickname, werewolf.Nickname));
        }

        foreach (var (dogTamerId, shouldBark) in context.DogTamerClues)
        {
            events.Add(new DogTamerClueEvent(dogTamerId, shouldBark));
        }

        // Trailer clues
        foreach (var (trailerId, dir) in context.TrailerClues)
        {
            events.Add(new TrailerClueEvent(trailerId, dir));
        }

        // Enshrouded players
        foreach (var enshroudedId in context.Enshrouded)
        {
            events.Add(new DraculaEnshroudedEvent(enshroudedId, enshroudedId)); // TODO: add the Dracula's ID to the event
        }

        // Peeper self kills
        foreach (var peeperId in context.PeeperKills)
        {
            killed.Add(peeperId);
        }

        // Spy ability
        if (context.SpyAbility is not null)
        {
            var (spyId, targetId) = context.SpyAbility.Value;
            var targetActions = actions.Where(a => a.Actor == targetId).ToList();
            events.Add(new SpyClueEvent(spyId, targetId, targetActions.Any()));
        }

        if (context.TripwireVertices is not null)
        {
            
            var positions = allPlayers
                .Select((player, index) => (player.Id, index))
                .ToDictionary(x => x.Id, x => x.index);

            var xIndex = positions[context.TripwireVertices.Value.PlayerA];
            var yIndex = positions[context.TripwireVertices.Value.PlayerB];

            int? GetArc(int index)
            {
                if (index == xIndex || index == yIndex)
                    return null;

                if (xIndex < yIndex)
                    return index > xIndex && index < yIndex ? 0 : 1;

                return index > xIndex || index < yIndex ? 0 : 1;
            }

            var count = actions.Count(action =>
            {
                var actorArc = GetArc(positions[action.Actor]);

                if (actorArc is null)
                    return false;

                return action.Targets.Any(targetId =>
                {
                    var targetArc = GetArc(positions[targetId]);

                    return targetArc.HasValue && targetArc.Value != actorArc.Value;
                });
            });
            events.Add(new RangerClueEvent(context.TripwireVertices.Value.Ranger, count));
        }

        if(context.AlchemistId is not null)
        {
            var alchemistId = context.AlchemistId.Value;
            var alchemist = alivePlayers.FirstOrDefault(p => p.Id == alchemistId);
            var nightsPassed = context.Game.Turns.Count();
            var clue = AlchemistBrewAbility.GetClue(nightsPassed, context, actions);
            events.Add(new AlchemistClueEvent(alchemistId, clue, clue));
        }

        // Include any events directly registered in context
        events.AddRange(context.Events);

        var deceiverEvents = context.Events.OfType<DeceiverEvent>().ToList();
        var deceiverTarget = deceiverEvents.Select(e => e.TargetId).FirstOrDefault();
        var clueEvents = events.OfType<ClueEvent>().ToList();
        var deceiverTargetClue = clueEvents.FirstOrDefault(e => e.ClueOwnerId == deceiverTarget);
        if (deceiverTargetClue != null)
        {
            deceiverTargetClue.ReverseClue();
        }

        foreach (var sleepingId in context.SleepingPlayers)
        {
            // sleeping players don't get clues, so remove any clue events for them
            var correspondingClueEvent = clueEvents.FirstOrDefault(e => e.ClueOwnerId == sleepingId);
            if (correspondingClueEvent != null)
            {
                events.Remove(correspondingClueEvent);
            }
        }

        var drunkPlayer = context.DrunkPlayer;
        if (drunkPlayer?.IsAlive == true)
        {
            var shouldEffectOccur = config!.Drunk!.DrunkOccuranceChance >= new Random().Next(100);
            var drunkPlayerClue = clueEvents.FirstOrDefault(e => e.ClueOwnerId == drunkPlayer.Id);
            if (shouldEffectOccur && drunkPlayerClue != null)
            {
                drunkPlayerClue.ReverseClue();
            }
        }

        return new NightResult
        {
            Events = events,
            Killed = killed,
            Actions = actions
        };
    }
}
