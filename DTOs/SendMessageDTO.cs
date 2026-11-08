namespace Connectify.API.DTOs;

public class SendMessageDTO
{
    public string? ConversationId { get; set; }
    public string? ChatId { get; set; }

    public string? RecipientId { get; set; }
    public string? ReceiverId { get; set; }

    public string? TextContent { get; set; }
    public string? Content { get; set; }
    public string? Message { get; set; }

    public string? AttachmentUrl { get; set; }
    public string? AttachmentType { get; set; }

    public string GetResolvedConversationId() =>
        !string.IsNullOrWhiteSpace(ConversationId) ? ConversationId.Trim() :
        !string.IsNullOrWhiteSpace(ChatId) ? ChatId.Trim() :
        string.Empty;

    public string GetResolvedRecipientId() =>
        !string.IsNullOrWhiteSpace(RecipientId) ? RecipientId.Trim() :
        !string.IsNullOrWhiteSpace(ReceiverId) ? ReceiverId.Trim() :
        string.Empty;

    public string GetResolvedTextContent() =>
        !string.IsNullOrWhiteSpace(TextContent) ? TextContent.Trim() :
        !string.IsNullOrWhiteSpace(Content) ? Content.Trim() :
        !string.IsNullOrWhiteSpace(Message) ? Message.Trim() :
        string.Empty;
}
