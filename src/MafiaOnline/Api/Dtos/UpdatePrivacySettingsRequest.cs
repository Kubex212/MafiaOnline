namespace MafiaOnline.Api.Dtos;

public class UpdatePrivacySettingsRequest
{
    public bool IsPrivate { get; set; }

    public string? Password { get; set; }
}
