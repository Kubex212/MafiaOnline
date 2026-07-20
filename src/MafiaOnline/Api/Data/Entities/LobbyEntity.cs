namespace Api.Data.Entities
{
    public class LobbyEntity
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public string StateJson { get; set; } = null!;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
