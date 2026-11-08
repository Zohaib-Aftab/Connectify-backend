namespace Connectify.API.DTOs;

public class PatchGroupDTO
{
    public string? GroupTitle { get; set; }
    public string? GroupName { get; set; }
    public string? Name { get; set; }

    public string? GroupPictureUrl { get; set; }
    public string? GroupIcon { get; set; }
    public string? Icon { get; set; }
    public string? PictureUrl { get; set; }

    public string? GetResolvedGroupName()
    {
        if (!string.IsNullOrWhiteSpace(GroupTitle)) return GroupTitle.Trim();
        if (!string.IsNullOrWhiteSpace(GroupName)) return GroupName.Trim();
        if (!string.IsNullOrWhiteSpace(Name)) return Name.Trim();
        return null;
    }

    public string? GetResolvedGroupIcon()
    {
        if (!string.IsNullOrWhiteSpace(GroupPictureUrl)) return GroupPictureUrl.Trim();
        if (!string.IsNullOrWhiteSpace(GroupIcon)) return GroupIcon.Trim();
        if (!string.IsNullOrWhiteSpace(Icon)) return Icon.Trim();
        if (!string.IsNullOrWhiteSpace(PictureUrl)) return PictureUrl.Trim();
        return null;
    }
}
