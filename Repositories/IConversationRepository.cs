using Connectify.API.Models;

namespace Connectify.API.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(string id);
    Task<Conversation?> GetPrivateConversationAsync(string user1Id, string user2Id);
    Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId);
    Task<Conversation> CreateAsync(Conversation conversation);
    Task<bool> UpdateLastMessageAsync(string conversationId, LastMessagePreview lastMessage);
    Task<bool> ExistsMemberAsync(string conversationId, string userId);
    Task<bool> AddMembersToGroupAsync(string conversationId, IEnumerable<string> memberIds);
    Task<bool> RemoveMemberFromGroupAsync(string conversationId, string memberId);
    Task<bool> RemoveMembersFromGroupAsync(string conversationId, IEnumerable<string> memberIds);
    Task<bool> UpdateGroupDetailsAsync(string conversationId, string? groupTitle, string? groupPictureUrl);
    Task<bool> DeleteConversationAsync(string conversationId);
}
