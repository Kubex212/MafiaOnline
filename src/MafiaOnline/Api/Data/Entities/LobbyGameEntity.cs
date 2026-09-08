namespace Api.Data.Entities;

public class LobbyGameEntity
{
    public Guid Id { get; set; }

    public Guid LobbyId { get; set; }

    public string LobbyCode { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }

    public DateTime FinishedAtUtc { get; set; }

    public string Winner { get; set; } = null!;

    public int PlayerCount { get; set; }

    public int DayCount { get; set; }

    public TimeSpan Duration { get; set; }

    public string ConfigJson { get; set; } = null!;
}