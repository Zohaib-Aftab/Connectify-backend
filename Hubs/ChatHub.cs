using System.Security.Claims;
using Connectify.API.DTOs;
using Connectify.API.Repositories;
using Connectify.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Connectify.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserConnectionManager _connectionManager;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageService messageService,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        IUserConnectionManager connectionManager,
        ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    private string CurrentUserId =>
        Context.UserIdentifier ??
        Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
        string.Empty;

    /// <summary>
    /// Invoked when a client establishes a real-time SignalR socket connection.
    /// Manages multi-terminal mapping and dynamically updates online presence in MongoDB Atlas.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        var connectionId = Context.ConnectionId;

        if (!string.IsNullOrEmpty(userId))
        {
            var isFirstConnection = _connectionManager.AddConnection(userId, connectionId);

            if (isFirstConnection)
            {
                // Update persistent MongoDB Atlas online flag
                await _userRepository.UpdateOnlineStatusAsync(userId, true);

                // Broadcast visibility state to all connected clients
                await Clients.Others.SendAsync("UserStatusChanged", new
                {
                    userId,
                    isOnline = true,
                    lastSeen = DateTime.UtcNow
                });

                _logger.LogInformation("User {UserId} entered ONLINE state (ConnectionId: {ConnectionId})", userId, connectionId);
            }

            // Transmit list of all active online user IDs to the connected caller
            var onlineUserIds = _connectionManager.GetAllOnlineUsers();
            await Clients.Caller.SendAsync("OnlineUsers", onlineUserIds);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Invoked when a client terminal socket disconnects.
    /// Deduplicates connection instances and updates MongoDB when the last active terminal closes.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;
        var connectionId = Context.ConnectionId;

        if (!string.IsNullOrEmpty(userId))
        {
            var isLastConnection = _connectionManager.RemoveConnection(userId, connectionId);

            if (isLastConnection)
            {
                // Update persistent MongoDB Atlas offline status and last seen timestamp
                await _userRepository.UpdateOnlineStatusAsync(userId, false);

                // Broadcast visibility state down the pipe to active clients
                await Clients.Others.SendAsync("UserStatusChanged", new
                {
                    userId,
                    isOnline = false,
                    lastSeen = DateTime.UtcNow
                });

                _logger.LogInformation("User {UserId} entered OFFLINE state (All terminal connections closed)", userId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Writes text message record to MongoDB storage first, then instantly dispatches a
    /// 'ReceiveMessage' execution frame down the receiver's socket stream.
    /// </summary>
    /// <param name="conversationId">Target conversation ID.</param>
    /// <param name="receiverId">Target receiver user ID.</param>
    /// <param name="messageContent">Text message content.</param>
    public async Task SendMessage(string? conversationId, string? receiverId, string messageContent)
    {
        var senderId = CurrentUserId;
        if (string.IsNullOrEmpty(senderId))
        {
            throw new HubException("Unauthorized: Sender identity could not be verified from token.");
        }

        if (string.IsNullOrWhiteSpace(messageContent))
        {
            throw new HubException("Message content cannot be empty.");
        }

        var sendDto = new SendMessageDTO
        {
            ConversationId = conversationId,
            RecipientId = receiverId,
            TextContent = messageContent
        };

        // 1. Commit message record into MongoDB Atlas storage layer first
        var savedMessage = await _messageService.SendMessageAsync(senderId, sendDto);

        // 2. Fetch conversation to determine participants for real-time dispatch
        var conversation = await _conversationRepository.GetByIdAsync(savedMessage.ConversationId);
        if (conversation != null && conversation.ConversationType == "Group")
        {
            // Deliver instantly to all group members across all active terminals
            await Clients.Users(conversation.MemberIds).SendAsync("ReceiveMessage", savedMessage);
            await Clients.Group($"conversation_{conversation.Id}").SendAsync("ReceiveMessage", savedMessage);
        }
        else
        {
            // Determine recipient ID for 1-to-1 conversation
            var targetReceiverId = !string.IsNullOrWhiteSpace(receiverId)
                ? receiverId
                : conversation?.MemberIds.FirstOrDefault(id => id != senderId) ?? string.Empty;

            if (!string.IsNullOrEmpty(targetReceiverId))
            {
                // Instantly dispatch 'ReceiveMessage' frame down the receiver's socket stream
                await Clients.User(targetReceiverId).SendAsync("ReceiveMessage", savedMessage);
            }

            // Sync back to sender's other open terminals/tabs
            await Clients.User(senderId).SendAsync("ReceiveMessage", savedMessage);
        }

        _logger.LogInformation("Message delivered via socket from {SenderId} in conversation {ConversationId}",
            senderId, savedMessage.ConversationId);
    }

    /// <summary>
    /// Instantly multi-casts state triggers to display 'User is typing...' on target receiver UI devices.
    /// </summary>
    /// <param name="receiverId">Recipient user ID.</param>
    /// <param name="isTyping">Boolean typing flag.</param>
    public async Task SendTypingStatus(string receiverId, bool isTyping)
    {
        var senderId = CurrentUserId;
        if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
            return;

        await Clients.User(receiverId).SendAsync("UserTyping", new
        {
            senderId,
            isTyping
        });
    }

    /// <summary>
    /// Updates read flag properties inside database logs and relays double-blue read receipts cleanly.
    /// </summary>
    /// <param name="messageId">ID of the read message.</param>
    /// <param name="senderId">Original author of the message who should receive the read receipt.</param>
    public async Task MarkAsRead(string messageId, string senderId)
    {
        var readerId = CurrentUserId;
        if (string.IsNullOrEmpty(readerId) || string.IsNullOrEmpty(messageId))
            return;

        // Update read flag properties inside database logs
        await _messageRepository.MarkSingleMessageAsReadAsync(messageId);

        // Relay double-blue read receipt acknowledgment to original message sender
        if (!string.IsNullOrEmpty(senderId))
        {
            await Clients.User(senderId).SendAsync("MessageRead", new
            {
                messageId,
                readerId
            });
        }
    }

    /// <summary>
    /// Marks all unread messages in a conversation as read and notifies the conversation participants.
    /// </summary>
    public async Task MarkConversationAsRead(string conversationId, string otherUserId)
    {
        var readerId = CurrentUserId;
        if (string.IsNullOrEmpty(readerId) || string.IsNullOrEmpty(conversationId))
            return;

        await _messageRepository.MarkAsReadAsync(conversationId, readerId);

        if (!string.IsNullOrEmpty(otherUserId))
        {
            await Clients.User(otherUserId).SendAsync("ConversationRead", new
            {
                conversationId,
                readerId
            });
        }
    }

    /// <summary>
    /// Allows a client terminal to subscribe to group conversation broadcast channels.
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        }
    }

    /// <summary>
    /// Allows a client terminal to unsubscribe from group conversation broadcast channels.
    /// </summary>
    public async Task LeaveConversation(string conversationId)
    {
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        }
    }
}
