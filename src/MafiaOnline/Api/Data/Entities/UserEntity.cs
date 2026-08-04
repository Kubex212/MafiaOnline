namespace Api.Data.Entities;

public class UserEntity
{
    public Guid Id { get; set; }
    public string? PreferredNickname { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
