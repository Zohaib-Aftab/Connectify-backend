using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Connectify.API.Models;

public class Message
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("conversationId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ConversationId { get; set; } = string.Empty;

    [BsonElement("senderId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string SenderId { get; set; } = string.Empty;

    [BsonElement("textContent")]
    public string TextContent { get; set; } = string.Empty;

    [BsonElement("attachmentUrl")]
    public string? AttachmentUrl { get; set; }

    [BsonElement("attachmentType")]
    public string? AttachmentType { get; set; } // "image", "file", "audio"

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("isRead")]
    public bool IsRead { get; set; } = false;

    [BsonElement("status")]
    public string Status { get; set; } = "Sent"; // "Sent", "Delivered", "Read"
}
