using MafiaOnline.Engine.Night;

namespace MafiaOnline.Api.Dtos;

public class NightActionRequest
{
    public Guid ActorId { get; set; }
    public List<Guid> Targets { get; set; } = new List<Guid>();
    public ActionType ActionType { get; set; } = ActionType.Ability; // if not set it's probably ability
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
