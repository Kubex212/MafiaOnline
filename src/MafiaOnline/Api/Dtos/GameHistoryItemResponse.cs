namespace Api.Dtos;

public class GameHistoryItemResponse
{
    public DateTime PlayedAtUtc { get; set; }

    public string Role { get; set; } = "";

    public bool Won { get; set; }

    public bool Survived { get; set; }

    public int PlayerCount { get; set; }

    public TimeSpan Duration { get; set; }
}
