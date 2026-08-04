namespace MafiaOnline.Api.Identity;

public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAuthenticated { get; }
}
