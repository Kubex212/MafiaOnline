using MafiaOnline.Engine.Night;

namespace MafiaOnline.Api.Dtos;

public class NightActionRequest
{
    public List<Guid> Targets { get; set; } = new List<Guid>();
    public ActionType ActionType { get; set; } = ActionType.Ability;
}

public class NightSkipRequest { }

public class DayVoteRequest
{
    public Guid CandidateId { get; set; }
}
