namespace MafiaOnline.Api.Dtos;

public class UpdateConfigRequest
{
    public GameConfigDto Config { get; set; } = new();
}
