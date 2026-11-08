using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Connectify.API.Models;

public class LastMessagePreview
{
    [BsonElement("textContent")]
    public string TextContent { get; set; } = string.Empty;

    [BsonElement("senderId")]
    public string SenderId { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("isRead")]
    public bool IsRead { get; set; } = false;
}

public class Conversation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("conversationType")]
    public string ConversationType { get; set; } = "Private"; // "Private" or "Group"

    [BsonElement("groupTitle")]
    public string? GroupTitle { get; set; }

    [BsonElement("groupPictureUrl")]
    public string? GroupPictureUrl { get; set; }

    [BsonElement("memberIds")]
    public List<string> MemberIds { get; set; } = [];

    [BsonElement("adminIds")]
    public List<string> AdminIds { get; set; } = [];

    [BsonElement("lastMessage")]
    public LastMessagePreview? LastMessage { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
