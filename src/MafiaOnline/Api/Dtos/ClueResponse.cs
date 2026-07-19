namespace MafiaOnline.Api.Dtos;

public record ClueResponse
{
    public string Type { get; init; }
    public Dictionary<string, object> Data { get; init; } = new Dictionary<string, object>();
}
