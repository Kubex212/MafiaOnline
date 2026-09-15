using MafiaOnline.Api.Models;

namespace MafiaOnline.Api.Repositories;

public interface ILobbyRepository
{
    Task Create(Lobby lobby);
    Task<Lobby?> Get(string code);
    Task Update(Lobby lobby);
    Task Delete(string code);

    // Return stored lobbies in pages (skip/take)
    Task<IReadOnlyList<Lobby>> GetAll(int skip = 0, int take = 100);

    /// <summary>
    /// Returns lobbies that are not started, not full, and have had activity since <paramref name="activeSince"/>.
    /// Used for the available-lobbies listing.
    /// </summary>
    Task<IReadOnlyList<Lobby>> GetAvailableAsync(DateTime activeSince, int skip = 0, int take = 100);

    /// <summary>
    /// Returns all lobbies whose last activity is older than <paramref name="cutoff"/>.
    /// Used by the cleanup background service.
    /// </summary>
    Task<IReadOnlyList<Lobby>> GetInactiveAsync(DateTime cutoff);
}
