using Connectify.API.Models;

namespace Connectify.API.DTOs;

public class GroupDTO
{
    public string Id { get; set; } = string.Empty;
    public string GroupTitle { get; set; } = string.Empty;
    public string GroupPictureUrl { get; set; } = string.Empty;
    public List<string> AdminIds { get; set; } = [];
    public List<string> MemberIds { get; set; } = [];
    public List<UserDTO> Members { get; set; } = [];
    public LastMessagePreview? LastMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
