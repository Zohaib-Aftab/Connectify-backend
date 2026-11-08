using Connectify.API.Models;

namespace Connectify.API.DTOs;

public class ConversationDTO
{
    public string Id { get; set; } = string.Empty;
    public string ConversationType { get; set; } = "Private";
    public string Title { get; set; } = string.Empty;
    public string PictureUrl { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = [];
    public UserDTO? Recipient { get; set; }
    public LastMessagePreview? LastMessage { get; set; }
    public int UnreadCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
