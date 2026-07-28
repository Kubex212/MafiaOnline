using MafiaOnline.Engine.Roles;

namespace MafiaOnline.Engine;

public class Player(Guid id, string nickname)
{
    public Guid Id { get; } = id;

    public string Nickname { get; } = nickname;

    // Allow JSON to set the alive flag when restoring state
    public bool IsAlive { get; set; } = true;

    public bool IsConnected { get; set; } = true;

    // Allow JSON to restore the assigned role
    public IRole Role { get; set; } = null!;

    /// <summary>
    /// The player's position around the table, assigned once at the start of the game.
    /// Night abilities with equal priority are resolved in ascending seat order.
    /// </summary>
    public int SeatIndex { get; set; }

    // Persist the last trap target set by a Poacher so MustAlternate can be enforced
    public Guid? LastPoacherTrapTarget { get; set; }

    public void AssignRole(IRole role)
    {
        Role = role;
    }

    public void Kill()
    {
        IsAlive = false;
    }

    public RoleType? Alibi { get; set; } = null;
}