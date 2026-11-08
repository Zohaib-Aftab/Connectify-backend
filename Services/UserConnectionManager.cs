using System.Collections.Concurrent;

namespace Connectify.API.Services;

public class UserConnectionManager : IUserConnectionManager
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();
    private readonly object _lock = new();

    public bool AddConnection(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId))
            return false;

        lock (_lock)
        {
            var connections = _userConnections.GetOrAdd(userId, _ => new HashSet<string>());
            var isFirstConnection = connections.Count == 0;
            connections.Add(connectionId);
            return isFirstConnection;
        }
    }

    public bool RemoveConnection(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId))
            return false;

        lock (_lock)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                    return true; // Transitioned to completely offline
                }
            }

            return false;
        }
    }

    public IReadOnlyCollection<string> GetConnections(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        lock (_lock)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                return connections.ToList();
            }

            return [];
        }
    }

    public bool IsUserOnline(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        lock (_lock)
        {
            return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }
    }

    public IReadOnlyCollection<string> GetAllOnlineUsers()
    {
        lock (_lock)
        {
            return _userConnections.Keys.ToList();
        }
    }
}
