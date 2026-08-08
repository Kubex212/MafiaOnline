using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Deceiver - selects a target to receive false information due to their role. E.g. trailer will receive opposite direcation that they should.
/// </summary>
public class Charmer : IRole, IHasNightAbility
{
    public RoleType Type => RoleType.Charmer;
    public Team Team => Team.Werewolves;
    public string Name => "Charmer";
    public int TeamId => 1;

    public NightAbility NightAbility { get; } = new CharmerSleepAbility();
}

public sealed class CharmerSleepAbility : NightAbility
{
    public CharmerSleepAbility() : base() { }
    protected override bool CanTargetSelf => true;
    protected override int TargetCooldown => 1;
    public override bool IsAutomatic => false;
    public override int MinTargets => 1;
    public override int MaxTargets => 1;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count == 0) return;
        var targetId = action.Targets[0];
        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId);
        if (target is null) return;

        context.SleepingPlayers.Add(targetId);
        context.AddEvent(new CharmerSleepEvent(actor.Id, targetId));
    }
}
