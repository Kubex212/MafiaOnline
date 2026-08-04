namespace MafiaOnline.Api.Identity;

public class CurrentUser : ICurrentUser
{
    private Guid? _userId;

    public Guid UserId => _userId ?? throw new InvalidOperationException("User is not authenticated.");
    public bool IsAuthenticated => _userId.HasValue;

    public void SetUserId(Guid userId) => _userId = userId;
}
