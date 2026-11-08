namespace Connectify.API.Services;

public interface IUserConnectionManager
{
    /// <summary>
    /// Tracks a new connection for a user.
    /// </summary>
    /// <returns>True if this is the user's first active terminal connection (transition from offline to online).</returns>
    bool AddConnection(string userId, string connectionId);

    /// <summary>
    /// Removes a connection for a user.
    /// </summary>
    /// <returns>True if the user has no remaining active terminal connections (transition from online to offline).</returns>
    bool RemoveConnection(string userId, string connectionId);

    /// <summary>
    /// Returns all connection IDs associated with a given user ID.
    /// </summary>
    IReadOnlyCollection<string> GetConnections(string userId);

    /// <summary>
    /// Determines whether the specified user currently has any active connections.
    /// </summary>
    bool IsUserOnline(string userId);

    /// <summary>
    /// Retrieves a list of all currently active online user IDs.
    /// </summary>
    IReadOnlyCollection<string> GetAllOnlineUsers();
}
