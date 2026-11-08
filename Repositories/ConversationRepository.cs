using Connectify.API.Models;
using MongoDB.Driver;

namespace Connectify.API.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly IMongoCollection<Conversation> _conversations;

    public ConversationRepository(IMongoDatabase database)
    {
        _conversations = database.GetCollection<Conversation>("Conversations");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        try
        {
            var memberIndex = new CreateIndexModel<Conversation>(
                Builders<Conversation>.IndexKeys.Ascending(c => c.MemberIds),
                new CreateIndexOptions { Background = true });

            var updatedAtIndex = new CreateIndexModel<Conversation>(
                Builders<Conversation>.IndexKeys.Descending(c => c.UpdatedAt),
                new CreateIndexOptions { Background = true });

            _conversations.Indexes.CreateMany([memberIndex, updatedAtIndex]);
        }
        catch
        {
            // Indexes may already exist
        }
    }

    public async Task<Conversation?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return await _conversations.Find(c => c.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Conversation?> GetPrivateConversationAsync(string user1Id, string user2Id)
    {
        if (string.IsNullOrWhiteSpace(user1Id) || string.IsNullOrWhiteSpace(user2Id))
            return null;

        var filter = Builders<Conversation>.Filter.And(
            Builders<Conversation>.Filter.Eq(c => c.ConversationType, "Private"),
            Builders<Conversation>.Filter.All(c => c.MemberIds, new[] { user1Id, user2Id }),
            Builders<Conversation>.Filter.Size(c => c.MemberIds, 2)
        );

        return await _conversations.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var filter = Builders<Conversation>.Filter.AnyEq(c => c.MemberIds, userId);
        return await _conversations.Find(filter)
            .SortByDescending(c => c.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Conversation> CreateAsync(Conversation conversation)
    {
        conversation.CreatedAt = DateTime.UtcNow;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _conversations.InsertOneAsync(conversation);
        return conversation;
    }

    public async Task<bool> UpdateLastMessageAsync(string conversationId, LastMessagePreview lastMessage)
    {
        var filter = Builders<Conversation>.Filter.Eq(c => c.Id, conversationId);
        var update = Builders<Conversation>.Update
            .Set(c => c.LastMessage, lastMessage)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        var result = await _conversations.UpdateOneAsync(filter, update);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> ExistsMemberAsync(string conversationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(userId))
            return false;

        var filter = Builders<Conversation>.Filter.And(
            Builders<Conversation>.Filter.Eq(c => c.Id, conversationId),
            Builders<Conversation>.Filter.AnyEq(c => c.MemberIds, userId)
        );

        var count = await _conversations.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<bool> AddMembersToGroupAsync(string conversationId, IEnumerable<string> memberIds)
    {
        var validIds = memberIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (validIds.Count == 0)
            return false;

        var filter = Builders<Conversation>.Filter.Eq(c => c.Id, conversationId);
        var update = Builders<Conversation>.Update
            .AddToSetEach(c => c.MemberIds, validIds)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        var result = await _conversations.UpdateOneAsync(filter, update);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> RemoveMemberFromGroupAsync(string conversationId, string memberId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(memberId))
            return false;

        var filter = Builders<Conversation>.Filter.Eq(c => c.Id, conversationId);
        var update = Builders<Conversation>.Update
            .Pull(c => c.MemberIds, memberId)
            .Pull(c => c.AdminIds, memberId)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        var result = await _conversations.UpdateOneAsync(filter, update);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> RemoveMembersFromGroupAsync(string conversationId, IEnumerable<string> memberIds)
    {
        var validIds = memberIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (validIds.Count == 0)
            return false;

        var filter = Builders<Conversation>.Filter.Eq(c => c.Id, conversationId);
        var update = Builders<Conversation>.Update
            .PullAll(c => c.MemberIds, validIds)
            .PullAll(c => c.AdminIds, validIds)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        var result = await _conversations.UpdateOneAsync(filter, update);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> UpdateGroupDetailsAsync(string conversationId, string? groupTitle, string? groupPictureUrl)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return false;

        var updates = new List<UpdateDefinition<Conversation>>();
        if (!string.IsNullOrWhiteSpace(groupTitle))
        {
            updates.Add(Builders<Conversation>.Update.Set(c => c.GroupTitle, groupTitle.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(groupPictureUrl))
        {
            updates.Add(Builders<Conversation>.Update.Set(c => c.GroupPictureUrl, groupPictureUrl.Trim()));
        }
        updates.Add(Builders<Conversation>.Update.Set(c => c.UpdatedAt, DateTime.UtcNow));

        var combinedUpdate = Builders<Conversation>.Update.Combine(updates);
        var filter = Builders<Conversation>.Filter.Eq(c => c.Id, conversationId);
        var result = await _conversations.UpdateOneAsync(filter, combinedUpdate);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> DeleteConversationAsync(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return false;

        var result = await _conversations.DeleteOneAsync(c => c.Id == conversationId);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }
}
