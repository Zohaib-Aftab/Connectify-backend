namespace Connectify.API.DTOs;

public class MessageDTO
{
    public string Id { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentType { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsRead { get; set; }
    public string Status { get; set; } = "Sent";
}
