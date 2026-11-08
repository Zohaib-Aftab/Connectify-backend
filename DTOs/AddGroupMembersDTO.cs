using System.ComponentModel.DataAnnotations;

namespace Connectify.API.DTOs;

public class AddGroupMembersDTO
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one member ID must be supplied.")]
    public List<string> MemberIds { get; set; } = [];
}
