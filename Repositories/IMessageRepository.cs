using Connectify.API.Models;

namespace Connectify.API.Repositories;

public interface IMessageRepository
{
    Task<Message> CreateAsync(Message message);
    Task<IEnumerable<Message>> GetByConversationIdAsync(string conversationId, int limit = 50, int skip = 0);
    Task<long> GetUnreadCountAsync(string conversationId, string recipientUserId);
    Task<long> MarkAsReadAsync(string conversationId, string readerUserId);
    Task<bool> MarkSingleMessageAsReadAsync(string messageId);
    Task<Message?> GetByIdAsync(string messageId);
}
