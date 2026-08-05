using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Dracula — werewolf-team role with active ability 'enshroud'.
/// Each night may enshroud one player; enshrouded players cannot be banished during the following day.
/// </summary>
public class Dracula : IRole, IHasNightAbility
{
    public RoleType Type => RoleType.Dracula;
    public Team Team => Team.Werewolves;
    public string Name => "Dracula";
    public int TeamId => 1;

    public NightAbility NightAbility { get; } = new DraculaEnshroudAbility();
}

public class DraculaEnshroudAbility : NightAbility
{
    public DraculaEnshroudAbility() : base() { }

    protected override bool CanTargetSelf => true;
    public override bool IsAutomatic => false;
    public override int MinTargets => 1;
    public override int MaxTargets => 1;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null || action.Targets.Count == 0) return;
        var targetId = action.Targets[0];
        var target = alivePlayers.FirstOrDefault(p => p.Id == targetId);
        if (target is null) return;

        context.Enshrouded.Add(targetId);
        // Add an event so that UI can learn about enshrouded targets if needed
        context.AddEvent(new DraculaEnshroudedEvent(actor.Id, targetId));
    }
}
