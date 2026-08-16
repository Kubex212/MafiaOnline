namespace MafiaOnline.Api.Dtos;

public class GameOverPayload
{
    public string Winner { get; set; } = string.Empty;
    public Guid? DrunkId { get; set; } = null;
    public string? DrunkName { get; set; } = null;
    public string? RematchCode { get; set; } = null;
}
