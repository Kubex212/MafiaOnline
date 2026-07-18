namespace MafiaOnline.Api.Dtos;

public class MovePlayerRequest
{
    public Guid RequestingPlayerId { get; set; }
    public Guid TargetPlayerId { get; set; }
}

public class ShufflePlayersRequest
{
    public Guid RequestingPlayerId { get; set; }
}
