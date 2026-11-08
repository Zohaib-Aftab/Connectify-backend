using Connectify.API.DTOs;

namespace Connectify.API.Services;

public interface IGroupService
{
    Task<GroupDTO> CreateGroupAsync(string creatorUserId, CreateGroupDTO dto);
    Task<GroupDTO> AddMembersAsync(string requesterUserId, string groupId, List<string> newMemberIds);
    Task<bool> RemoveMemberAsync(string requesterUserId, string groupId, string memberId);
    Task<GroupDTO> RemoveMembersAsync(string requesterUserId, string groupId, List<string> memberIds);
    Task<bool> LeaveGroupAsync(string userId, string groupId);
    Task<GroupDTO> PatchGroupAsync(string requesterUserId, string groupId, PatchGroupDTO dto);
    Task<bool> DeleteGroupAsync(string requesterUserId, string groupId);
    Task<GroupDTO?> GetGroupDetailsAsync(string requesterUserId, string groupId);
}
