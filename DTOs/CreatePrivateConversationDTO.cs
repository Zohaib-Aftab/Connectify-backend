namespace Connectify.API.DTOs;

public class CreatePrivateConversationDTO
{
    public string? RecipientId { get; set; }
    public string? UserId { get; set; }
    public string? MemberId { get; set; }

    public string GetTargetUserId()
    {
        if (!string.IsNullOrWhiteSpace(RecipientId)) return RecipientId.Trim();
        if (!string.IsNullOrWhiteSpace(UserId)) return UserId.Trim();
        if (!string.IsNullOrWhiteSpace(MemberId)) return MemberId.Trim();
        return string.Empty;
    }
}
