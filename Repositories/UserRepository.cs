using System.Text.RegularExpressions;
using Connectify.API.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Connectify.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public UserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("Users");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        try
        {
            var emailIndex = new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Background = true, Sparse = true });

            var usernameIndex = new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Background = true, Sparse = true });

            _users.Indexes.CreateMany([emailIndex, usernameIndex]);
        }
        catch
        {
            // Index creation can be ignored if already created or offline in test environments
        }
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var filter = Builders<User>.Filter.Eq(u => u.Id, id);
        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var validIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (validIds.Count == 0)
            return [];

        var filter = Builders<User>.Filter.In(u => u.Id, validIds);
        return await _users.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<User>> GetAllAsync(int limit = 100, int skip = 0)
    {
        return await _users.Find(Builders<User>.Filter.Empty)
            .SortByDescending(u => u.LastSeen)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var filter = Builders<User>.Filter.Regex(
            u => u.Email,
            new BsonRegularExpression($"^{Regex.Escape(email.Trim())}$", "i"));

        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var filter = Builders<User>.Filter.Regex(
            u => u.Username,
            new BsonRegularExpression($"^{Regex.Escape(username.Trim())}$", "i"));

        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmailOrUsernameAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var cleanIdentifier = Regex.Escape(identifier.Trim());
        var emailFilter = Builders<User>.Filter.Regex(
            u => u.Email,
            new BsonRegularExpression($"^{cleanIdentifier}$", "i"));

        var usernameFilter = Builders<User>.Filter.Regex(
            u => u.Username,
            new BsonRegularExpression($"^{cleanIdentifier}$", "i"));

        var combinedFilter = Builders<User>.Filter.Or(emailFilter, usernameFilter);
        return await _users.Find(combinedFilter).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<User>> SearchByUsernameAsync(string query)
    {
        return await SearchUsersAsync(query);
    }

    public async Task<IEnumerable<User>> SearchUsersAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var regex = new BsonRegularExpression(Regex.Escape(query.Trim()), "i");
        var filter = Builders<User>.Filter.Or(
            Builders<User>.Filter.Regex(u => u.Username, regex),
            Builders<User>.Filter.Regex(u => u.FullName, regex)
        );

        return await _users.Find(filter).Limit(25).ToListAsync();
    }

    public async Task<bool> UpdateProfileAsync(string id, string? fullName, string? bio, string? profilePictureUrl)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var updates = new List<UpdateDefinition<User>>();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            updates.Add(Builders<User>.Update.Set(u => u.FullName, fullName.Trim()));
        }
        if (bio != null)
        {
            updates.Add(Builders<User>.Update.Set(u => u.Bio, bio.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(profilePictureUrl))
        {
            updates.Add(Builders<User>.Update.Set(u => u.ProfilePictureUrl, profilePictureUrl.Trim()));
        }
        updates.Add(Builders<User>.Update.Set(u => u.UpdatedAt, DateTime.UtcNow));

        var combinedUpdate = Builders<User>.Update.Combine(updates);
        var result = await _users.UpdateOneAsync(u => u.Id == id, combinedUpdate);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<User> CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        user.Email = user.Email.Trim().ToLowerInvariant();
        user.Username = user.Username.Trim();

        await _users.InsertOneAsync(user);
        return user;
    }

    public async Task<bool> UpdateAsync(string id, User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<User>.Filter.Eq(u => u.Id, id);
        var result = await _users.ReplaceOneAsync(filter, user);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var filter = Builders<User>.Filter.Regex(
            u => u.Email,
            new BsonRegularExpression($"^{Regex.Escape(email.Trim())}$", "i"));

        var count = await _users.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        var filter = Builders<User>.Filter.Regex(
            u => u.Username,
            new BsonRegularExpression($"^{Regex.Escape(username.Trim())}$", "i"));

        var count = await _users.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<bool> UpdateOnlineStatusAsync(string id, bool isOnline)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, id);
        var update = Builders<User>.Update
            .Set(u => u.IsOnline, isOnline)
            .Set(u => u.LastSeen, DateTime.UtcNow);

        var result = await _users.UpdateOneAsync(filter, update);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }
}
