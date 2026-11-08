using Connectify.API.DTOs;

namespace Connectify.API.Services;

public interface IMessageService
{
    Task<ConversationDTO> GetOrCreatePrivateConversationAsync(string currentUserId, string recipientId);
    Task<IEnumerable<ConversationDTO>> GetUserConversationsAsync(string currentUserId);
    Task<ConversationDTO?> GetConversationByIdAsync(string conversationId, string currentUserId);
    Task<IEnumerable<MessageDTO>> GetConversationMessagesAsync(string conversationId, string currentUserId, int limit = 50, int skip = 0);
    Task<MessageDTO> SendMessageAsync(string currentUserId, SendMessageDTO dto);
}
