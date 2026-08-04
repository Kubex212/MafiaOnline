namespace Api.Dtos
{
    public class MeResponse
    {
        public Guid Id { get; set; }

        public string? PreferredNickname { get; set; }

        public bool IsAnonymous { get; set; }

        public List<string> LinkedProviders { get; set; } = new();
    }

    public class UpdateMeRequest
    {
        public string? PreferredNickname { get; set; }
    }
}
