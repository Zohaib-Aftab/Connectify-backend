using Connectify.API.Models;
using MongoDB.Driver;

namespace Connectify.API.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly IMongoCollection<Message> _messages;

    public MessageRepository(IMongoDatabase database)
    {
        _messages = database.GetCollection<Message>("Messages");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        try
        {
            var conversationTimestampIndex = new CreateIndexModel<Message>(
                Builders<Message>.IndexKeys
                    .Ascending(m => m.ConversationId)
                    .Descending(m => m.Timestamp),
                new CreateIndexOptions { Background = true });

            _messages.Indexes.CreateOne(conversationTimestampIndex);
        }
        catch
        {
            // Index may already exist
        }
    }

    public async Task<Message> CreateAsync(Message message)
    {
        message.Timestamp = DateTime.UtcNow;
        if (string.IsNullOrEmpty(message.Status))
        {
            message.Status = "Sent";
        }
        await _messages.InsertOneAsync(message);
        return message;
    }

    public async Task<IEnumerable<Message>> GetByConversationIdAsync(string conversationId, int limit = 50, int skip = 0)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return [];

        var filter = Builders<Message>.Filter.Eq(m => m.ConversationId, conversationId);

        // Fetch latest messages descending, then order chronologically for client rendering
        var messages = await _messages.Find(filter)
            .SortByDescending(m => m.Timestamp)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();

        return messages.OrderBy(m => m.Timestamp).ToList();
    }

    public async Task<long> GetUnreadCountAsync(string conversationId, string recipientUserId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(recipientUserId))
            return 0;

        var filter = Builders<Message>.Filter.And(
            Builders<Message>.Filter.Eq(m => m.ConversationId, conversationId),
            Builders<Message>.Filter.Ne(m => m.SenderId, recipientUserId),
            Builders<Message>.Filter.Eq(m => m.IsRead, false)
        );

        return await _messages.CountDocumentsAsync(filter);
    }

    public async Task<long> MarkAsReadAsync(string conversationId, string readerUserId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(readerUserId))
            return 0;

        var filter = Builders<Message>.Filter.And(
            Builders<Message>.Filter.Eq(m => m.ConversationId, conversationId),
            Builders<Message>.Filter.Ne(m => m.SenderId, readerUserId),
            Builders<Message>.Filter.Eq(m => m.IsRead, false)
        );

        var update = Builders<Message>.Update
            .Set(m => m.IsRead, true)
            .Set(m => m.Status, "Read");

        var result = await _messages.UpdateManyAsync(filter, update);
        return result.ModifiedCount;
    }

    public async Task<bool> MarkSingleMessageAsReadAsync(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return false;

        var filter = Builders<Message>.Filter.Eq(m => m.Id, messageId);
        var update = Builders<Message>.Update
            .Set(m => m.IsRead, true)
            .Set(m => m.Status, "Read");

        var result = await _messages.UpdateOneAsync(filter, update);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<Message?> GetByIdAsync(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        return await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
    }
}
