using System.ComponentModel.DataAnnotations;

namespace Connectify.API.DTOs;

public class UpdateProfileDTO
{
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    public string? FullName { get; set; }

    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    public string? Name { get; set; }

    [StringLength(250, ErrorMessage = "Bio cannot exceed 250 characters.")]
    public string? Bio { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public string? GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(FullName)) return FullName.Trim();
        if (!string.IsNullOrWhiteSpace(Name)) return Name.Trim();
        return null;
    }
}
