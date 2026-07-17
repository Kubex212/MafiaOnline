namespace MafiaOnline.Api.Utils;

public static class LobbyCodeGenerator
{
    private const string Characters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var random = Random.Shared;

        return new string(
            Enumerable.Range(0, 6)
                .Select(_ => Characters[random.Next(Characters.Length)])
                .ToArray());
    }
}