namespace MafiaOnline.Api.Dtos;

/// <summary>
/// Private response sent only to the Detective after night resolution.
/// </summary>
public class DetectiveClueResponse
{
    public string SubjectNickname { get; set; } = "";
    public bool IsWerewolf { get; set; }
}
