using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

public class Innkeeper : IRole, IHasNightAbility
{
    private readonly InnkeeperConfig _config;

    public Innkeeper(InnkeeperConfig? config = null)
    {
        _config = config ?? new InnkeeperConfig();
        NightAbility = new InnkeeperAbility(_config);
    }

    public RoleType Type => RoleType.Innkeeper;
    public Team Team => Team.Villagers;
    public string Name => "Innkeeper";
    public int TeamId => 0;
    public NightAbility NightAbility { get; }
}

public class InnkeeperAbility : NightAbility
{
    private readonly InnkeeperConfig _config;

    public InnkeeperAbility(InnkeeperConfig config)
    {
        _config = config;
    }

    public override bool IsAutomatic => false;
    public override int MinTargets => 1;
    public override int MaxTargets => 1;

    public override void Apply(Player actor, NightAction? action, NightContext context, IReadOnlyList<Player> alivePlayers)
    {
        if (action is null) return;
        var target = action.Targets.FirstOrDefault();
        if (target == Guid.Empty) return;

        context.AddEvent(new InnkeeperSelectionEvent(actor.Id, target));
    }
}

public sealed class InnkeeperSelectionEvent : NightEvent
{
    public Guid InnkeeperId { get; }
    public Guid SelectedId { get; }

    public InnkeeperSelectionEvent(Guid innkeeperId, Guid selectedId) => (InnkeeperId, SelectedId) = (innkeeperId, selectedId);
}
