namespace MafiaOnline.Api.Dtos;

public class NightActionRequest
{
    public Guid ActorId { get; set; }
    public List<Guid> Targets { get; set; } = new List<Guid>();
}

public class NightSkipRequest
{
    public Guid ActorId { get; set; }
}

public class DayVoteRequest
{
    public Guid VoterId { get; set; }
    public Guid CandidateId { get; set; }
}
