using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Connectify.API.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("fullName")]
    [Required]
    public string FullName { get; set; } = string.Empty;

    [BsonElement("username")]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BsonElement("email")]
    [Required]
    public string Email { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("profilePictureUrl")]
    public string ProfilePictureUrl { get; set; } = string.Empty;

    [BsonElement("bio")]
    public string Bio { get; set; } = "Hey there! I am using Connectify.";

    [BsonElement("lastSeen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    [BsonElement("isOnline")]
    public bool IsOnline { get; set; } = false;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
