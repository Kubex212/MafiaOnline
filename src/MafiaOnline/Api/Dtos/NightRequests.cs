namespace MafiaOnline.Api.Dtos;

public class NightActionRequest
{
    public Guid ActorId { get; set; }
    public Guid TargetId { get; set; }
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
