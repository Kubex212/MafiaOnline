using MafiaOnline.Engine.Roles;

namespace Api.Data.Entities;

public class GameHistoryEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid LobbyId { get; set; }

    public string LobbyCode { get; set; } = null!;

    public Guid GameId { get; set; }

    public DateTime PlayedAtUtc { get; set; }

    public RoleType Role { get; set; }

    public int TeamId { get; set; }

    public bool Won { get; set; }

    public bool Survived { get; set; }

    public int PlayerCount { get; set; }

    public int DayCount { get; set; }

    public TimeSpan Duration { get; set; }
}
