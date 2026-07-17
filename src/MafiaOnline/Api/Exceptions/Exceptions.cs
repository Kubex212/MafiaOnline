namespace Api.Exceptions
{
    public class LobbyNotFoundException : Exception
    {
        public LobbyNotFoundException(string code)
            : base($"Lobby '{code}' was not found.")
        {
        }
    }

    public class DuplicateNicknameException : Exception
    {
        public DuplicateNicknameException(string nickname)
            : base($"Nickname '{nickname}' is already taken.")
        {
        }
    }
}
