using MafiaOnline.Engine.Roles;

namespace MafiaOnline.Engine;

public class Player(Guid id, string nickname)
{
    public Guid Id { get; } = id;

    public string Nickname { get; } = nickname;

    public bool IsAlive { get; private set; } = true;

    public bool IsConnected { get; set; } = true;

    public IRole Role { get; private set; } = null!;

    /// <summary>
    /// The player's position around the table, assigned once at the start of the game.
    /// Night abilities with equal priority are resolved in ascending seat order.
    /// </summary>
    public int SeatIndex { get; internal set; }

    public void AssignRole(IRole role)
    {
        Role = role;
    }

    public void Kill()
    {
        IsAlive = false;
    }
}