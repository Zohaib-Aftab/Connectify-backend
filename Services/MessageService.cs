using Connectify.API.DTOs;
using Connectify.API.Models;
using Connectify.API.Repositories;

namespace Connectify.API.Services;

public class MessageService : IMessageService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IUserRepository userRepository,
        ILogger<MessageService> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ConversationDTO> GetOrCreatePrivateConversationAsync(string currentUserId, string recipientId)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            throw new ArgumentException("Recipient user ID cannot be empty.");
        }

        if (currentUserId == recipientId)
        {
            throw new ArgumentException("Cannot start a conversation with yourself.");
        }

        var recipient = await _userRepository.GetByIdAsync(recipientId);
        if (recipient == null)
        {
            throw new KeyNotFoundException("Recipient user not found.");
        }

        var conversation = await _conversationRepository.GetPrivateConversationAsync(currentUserId, recipientId);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                ConversationType = "Private",
                MemberIds = [currentUserId, recipientId],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            conversation = await _conversationRepository.CreateAsync(conversation);
            _logger.LogInformation("Created new private conversation between {User1} and {User2}", currentUserId, recipientId);
        }

        var unreadCount = await _messageRepository.GetUnreadCountAsync(conversation.Id!, currentUserId);

        return new ConversationDTO
        {
            Id = conversation.Id ?? string.Empty,
            ConversationType = conversation.ConversationType,
            Title = recipient.FullName,
            PictureUrl = recipient.ProfilePictureUrl,
            MemberIds = conversation.MemberIds,
            Recipient = MapUserDto(recipient),
            LastMessage = conversation.LastMessage,
            UnreadCount = (int)unreadCount,
            UpdatedAt = conversation.UpdatedAt
        };
    }

    public async Task<IEnumerable<ConversationDTO>> GetUserConversationsAsync(string currentUserId)
    {
        var conversations = (await _conversationRepository.GetUserConversationsAsync(currentUserId)).ToList();
        if (conversations.Count == 0)
            return [];

        // Collect all distinct other user IDs across conversations
        var otherUserIds = conversations
            .SelectMany(c => c.MemberIds.Where(id => id != currentUserId))
            .Distinct()
            .ToList();

        var users = await _userRepository.GetByIdsAsync(otherUserIds);
        var userDict = users.ToDictionary(u => u.Id ?? string.Empty, MapUserDto);

        var result = new List<ConversationDTO>();

        foreach (var conv in conversations)
        {
            var otherUserId = conv.MemberIds.FirstOrDefault(id => id != currentUserId) ?? string.Empty;
            userDict.TryGetValue(otherUserId, out var recipient);

            var unreadCount = await _messageRepository.GetUnreadCountAsync(conv.Id!, currentUserId);

            var title = conv.ConversationType == "Group"
                ? (conv.GroupTitle ?? "Group Chat")
                : (recipient?.FullName ?? "Unknown Contact");

            var picture = conv.ConversationType == "Group"
                ? (conv.GroupPictureUrl ?? "")
                : (recipient?.ProfilePictureUrl ?? "");

            result.Add(new ConversationDTO
            {
                Id = conv.Id ?? string.Empty,
                ConversationType = conv.ConversationType,
                Title = title,
                PictureUrl = picture,
                MemberIds = conv.MemberIds,
                Recipient = recipient,
                LastMessage = conv.LastMessage,
                UnreadCount = (int)unreadCount,
                UpdatedAt = conv.UpdatedAt
            });
        }

        return result;
    }

    public async Task<ConversationDTO?> GetConversationByIdAsync(string conversationId, string currentUserId)
    {
        var conv = await _conversationRepository.GetByIdAsync(conversationId);
        if (conv == null)
            return null;

        if (!conv.MemberIds.Contains(currentUserId))
        {
            throw new UnauthorizedAccessException("Access denied: You are not a member of this conversation.");
        }

        var isGroup = conv.ConversationType == "Group";
        UserDTO? recipient = null;

        if (!isGroup)
        {
            var otherUserId = conv.MemberIds.FirstOrDefault(id => id != currentUserId);
            if (!string.IsNullOrEmpty(otherUserId))
            {
                var user = await _userRepository.GetByIdAsync(otherUserId);
                if (user != null)
                {
                    recipient = MapUserDto(user);
                }
            }
        }

        var unreadCount = await _messageRepository.GetUnreadCountAsync(conv.Id!, currentUserId);
        var title = isGroup ? (conv.GroupTitle ?? "Group Chat") : (recipient?.FullName ?? "Unknown Contact");
        var picture = isGroup ? (conv.GroupPictureUrl ?? "") : (recipient?.ProfilePictureUrl ?? "");

        return new ConversationDTO
        {
            Id = conv.Id ?? string.Empty,
            ConversationType = conv.ConversationType,
            Title = title,
            PictureUrl = picture,
            MemberIds = conv.MemberIds,
            Recipient = recipient,
            LastMessage = conv.LastMessage,
            UnreadCount = (int)unreadCount,
            UpdatedAt = conv.UpdatedAt
        };
    }

    public async Task<IEnumerable<MessageDTO>> GetConversationMessagesAsync(string conversationId, string currentUserId, int limit = 50, int skip = 0)
    {
        var isMember = await _conversationRepository.ExistsMemberAsync(conversationId, currentUserId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not a participant in this conversation.");
        }

        // Mark incoming unread messages as read
        await _messageRepository.MarkAsReadAsync(conversationId, currentUserId);

        var messages = await _messageRepository.GetByConversationIdAsync(conversationId, limit, skip);

        return messages.Select(MapMessageDto).ToList();
    }

    public async Task<MessageDTO> SendMessageAsync(string currentUserId, SendMessageDTO dto)
    {
        string conversationId = dto.GetResolvedConversationId();
        string recipientId = dto.GetResolvedRecipientId();
        string textContent = dto.GetResolvedTextContent();

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            if (string.IsNullOrWhiteSpace(recipientId))
            {
                throw new ArgumentException("Either ConversationId (ChatId) or RecipientId (ReceiverId) must be specified.");
            }

            var conv = await GetOrCreatePrivateConversationAsync(currentUserId, recipientId);
            conversationId = conv.Id;
        }
        else
        {
            var isMember = await _conversationRepository.ExistsMemberAsync(conversationId, currentUserId);
            if (!isMember)
            {
                throw new UnauthorizedAccessException("You are not authorized to send messages to this conversation.");
            }
        }

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = currentUserId,
            TextContent = textContent,
            AttachmentUrl = dto.AttachmentUrl,
            AttachmentType = dto.AttachmentType,
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            Status = "Sent"
        };

        var created = await _messageRepository.CreateAsync(message);

        // Update conversation last message snippet
        var lastMessagePreview = new LastMessagePreview
        {
            TextContent = string.IsNullOrWhiteSpace(created.TextContent)
                ? (string.IsNullOrEmpty(created.AttachmentType) ? "Attachment" : $"[{created.AttachmentType}]")
                : created.TextContent,
            SenderId = currentUserId,
            Timestamp = created.Timestamp,
            IsRead = false
        };

        await _conversationRepository.UpdateLastMessageAsync(conversationId, lastMessagePreview);

        return MapMessageDto(created);
    }

    private static UserDTO MapUserDto(User user)
    {
        return new UserDTO
        {
            Id = user.Id ?? string.Empty,
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            ProfilePictureUrl = user.ProfilePictureUrl,
            Bio = user.Bio,
            LastSeen = user.LastSeen,
            IsOnline = user.IsOnline,
            CreatedAt = user.CreatedAt
        };
    }

    private static MessageDTO MapMessageDto(Message m)
    {
        return new MessageDTO
        {
            Id = m.Id ?? string.Empty,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            TextContent = m.TextContent,
            AttachmentUrl = m.AttachmentUrl,
            AttachmentType = m.AttachmentType,
            Timestamp = m.Timestamp,
            IsRead = m.IsRead,
            Status = m.Status
        };
    }
}
