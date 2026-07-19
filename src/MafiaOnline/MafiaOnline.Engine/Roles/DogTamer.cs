using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;

namespace MafiaOnline.Engine.Roles;

/// <summary>
/// Detective (citizen) — each night is automatically pointed at a random living player
/// (other than themselves) and receives their alignment (innocent / werewolf).
/// </summary>
public class DogTamer : IRole, IHasNightAbility
{
    public DogTamer(DogTamerConfig? config = null)
    {
        NightAbility = new DogTamerAbility(config ?? new DogTamerConfig());
    }

    public RoleType Type => RoleType.DogTamer;
    public string Name => "Dog tamer";
    public int TeamId => 0;
    public NightAbility NightAbility { get; }
}