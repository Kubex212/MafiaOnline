using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Alchemist (citizen) — once per game can brew a potion - the longer he waited the better clue he obtains from it.
/// </summary>
public class Alchemist : IRole, IHasNightAbility
{
    public Alchemist()
    {
        NightAbility = new AlchemistBrewAbility();
    }

    public RoleType Type => RoleType.Alchemist;
    public Team Team => Team.Villagers;
    public string Name => "Alchemist";
    public int TeamId => 0;

    public NightAbility NightAbility { get; }
}

public sealed class AlchemistBrewAbility() : NightAbility()
{
    protected override bool CanTargetSelf => false;
    public override bool IsAutomatic => false;
    public override int MinTargets => 0;
    public override int MaxTargets => 0;
    public override int? Cooldown => 999; // once per game

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count != 0) return;
        
        context.AlchemistId = actor.Id;
    }

    public static AlchemistClue GetClue(int nightsPassed, NightContext context, IReadOnlyList<NightAction> actions)
    {
        var rolesWithActiveAbility = context.Game.Players.Select(p => p.Role).Count(r => r is IHasNightAbility);

        var rolesPool = context.Game.Players
            .Where(p => p.Role.Team == Team.Villagers)
            .Select(p => p.RoleType)
            .ToList();
        rolesPool.AddRange(context.Game.Players.Where(p => p.Role.Team == Team.Werewolves && p.Alibi.HasValue).Select(p => p.Alibi!.Value).ToList());

        var rolesWithNeutralPool = new List<RoleType>(rolesPool);
        rolesWithNeutralPool.AddRange(context.Game.Players.Where(p => p.Role.Team == Team.Neutral).Select(p => p.RoleType).ToList());

        var werewolfRoles = context.Game.Players
            .Where(p => p.Role.Team == Team.Werewolves)
            .Select(p => p.RoleType)
            .ToList();

        var werewolves = context.Game.Players.Where(p => p.Role.Team == Team.Werewolves).Select(p => p.Nickname).ToList();

        object data = nightsPassed switch
        {
            1 => rolesWithActiveAbility,
            2 => rolesPool,
            3 => rolesWithNeutralPool,
            4 => werewolfRoles,
            _ => werewolves
        };

        return new AlchemistClue
        {
            ClueBase = $"alchemistClue" + Math.Min(nightsPassed, 5),
            Data = data,
            NightsPassed = nightsPassed
        };
    }

    public class AlchemistClue
    {
        public string ClueBase { get; set; } = string.Empty;
        public int NightsPassed { get; set; } = 1;
        public object Data { get; set; } = string.Empty;
    }
}
