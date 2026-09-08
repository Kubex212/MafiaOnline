namespace Api.Dtos;

public class LobbyGameHistoryItemResponse
{
    public Guid GameId { get; set; }

    public string LobbyCode { get; set; } = "";

    public DateTime StartedAtUtc { get; set; }

    public DateTime FinishedAtUtc { get; set; }

    public string Winner { get; set; } = "";

    public int PlayerCount { get; set; }

    public int DayCount { get; set; }

    public TimeSpan Duration { get; set; }
}