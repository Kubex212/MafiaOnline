using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Core;
using MafiaOnline.Engine.Roles;
using Newtonsoft.Json;

namespace MafiaOnline.Engine;

public class Player(Guid id, string nickname)
{
    public Guid Id { get; } = id;

    public string Nickname { get; } = nickname;

    // Allow JSON to set the alive flag when restoring state
    public bool IsAlive { get; set; } = true;

    public bool IsConnected { get; set; } = true;

    [JsonProperty]
    public RoleType RoleType { get; private set; }

    [JsonIgnore]
    public IRole Role { get; private set; }


    /// <summary>
    /// The player's position around the table, assigned once at the start of the game.
    /// Night abilities with equal priority are resolved in ascending seat order.
    /// </summary>
    public int SeatIndex { get; set; }

    // Persist the last trap target set by a Poacher so MustAlternate can be enforced
    public Guid? LastPoacherTrapTarget { get; set; }

    public void Kill()
    {
        IsAlive = false;
    }
    public void InitializeRole(RoleType type, GameConfig config)
    {
        RoleType = type;
        Role = RoleFactory.Create(type, config);
    }

    public RoleType? Alibi { get; set; } = null;
}