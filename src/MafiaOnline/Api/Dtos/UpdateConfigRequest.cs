namespace MafiaOnline.Api.Dtos;

public class UpdateConfigRequest
{
    public Guid RequestingPlayerId { get; set; }

    public GameConfigDto Config { get; set; } = new();
}
