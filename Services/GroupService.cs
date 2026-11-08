using Connectify.API.DTOs;
using Connectify.API.Hubs;
using Connectify.API.Models;
using Connectify.API.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace Connectify.API.Services;

public class GroupService : IGroupService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<GroupService> _logger;

    public GroupService(
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        IHubContext<ChatHub> hubContext,
        ILogger<GroupService> logger)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<GroupDTO> CreateGroupAsync(string creatorUserId, CreateGroupDTO dto)
    {
        var groupName = dto.GetResolvedGroupName();
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new ArgumentException("Group name cannot be empty.");
        }

        var memberSet = new HashSet<string>(dto.MemberIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            creatorUserId
        };

        var adminSet = new HashSet<string>(dto.AdminIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            creatorUserId
        };

        foreach (var admin in adminSet)
        {
            memberSet.Add(admin);
        }

        var memberIds = memberSet.ToList();
        var adminIds = adminSet.ToList();

        var resolvedIcon = dto.GetResolvedGroupIcon();
        var groupPictureUrl = string.IsNullOrWhiteSpace(resolvedIcon)
            ? $"https://api.dicebear.com/7.x/identicon/svg?seed={Uri.EscapeDataString(groupName)}"
            : resolvedIcon;

        var conversation = new Conversation
        {
            ConversationType = "Group",
            GroupTitle = groupName,
            GroupPictureUrl = groupPictureUrl,
            MemberIds = memberIds,
            AdminIds = adminIds,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdGroup = await _conversationRepository.CreateAsync(conversation);
        var users = await _userRepository.GetByIdsAsync(memberIds);
        var membersDto = users.Select(MapUserDto).ToList();

        var groupDto = new GroupDTO
        {
            Id = createdGroup.Id ?? string.Empty,
            GroupTitle = createdGroup.GroupTitle ?? string.Empty,
            GroupPictureUrl = createdGroup.GroupPictureUrl ?? string.Empty,
            AdminIds = createdGroup.AdminIds,
            MemberIds = createdGroup.MemberIds,
            Members = membersDto,
            LastMessage = null,
            CreatedAt = createdGroup.CreatedAt,
            UpdatedAt = createdGroup.UpdatedAt
        };

        // Instantly sync group creation to all member sessions via SignalR
        await _hubContext.Clients.Users(memberIds).SendAsync("GroupCreated", groupDto);

        _logger.LogInformation("New group chat '{GroupTitle}' created by {CreatorId} with {Count} members and {Admins} admins",
            groupDto.GroupTitle, creatorUserId, memberIds.Count, adminIds.Count);

        return groupDto;
    }

    public async Task<GroupDTO> AddMembersAsync(string requesterUserId, string groupId, List<string> newMemberIds)
    {
        var group = await _conversationRepository.GetByIdAsync(groupId);
        if (group == null || group.ConversationType != "Group")
        {
            throw new KeyNotFoundException("Group conversation not found.");
        }

        if (!group.MemberIds.Contains(requesterUserId))
        {
            throw new UnauthorizedAccessException("You must be an active member of the group to add participants.");
        }

        var distinctNewIds = newMemberIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && !group.MemberIds.Contains(id))
            .Distinct()
            .ToList();

        if (distinctNewIds.Count > 0)
        {
            await _conversationRepository.AddMembersToGroupAsync(groupId, distinctNewIds);
            group.MemberIds.AddRange(distinctNewIds);
        }

        var allUsers = await _userRepository.GetByIdsAsync(group.MemberIds);
        var membersDto = allUsers.Select(MapUserDto).ToList();

        var updatedGroupDto = new GroupDTO
        {
            Id = group.Id ?? string.Empty,
            GroupTitle = group.GroupTitle ?? string.Empty,
            GroupPictureUrl = group.GroupPictureUrl ?? string.Empty,
            AdminIds = group.AdminIds,
            MemberIds = group.MemberIds,
            Members = membersDto,
            LastMessage = group.LastMessage,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt
        };

        // Notify all current and newly added members via SignalR
        await _hubContext.Clients.Users(group.MemberIds).SendAsync("GroupMemberAdded", new
        {
            groupId,
            addedMemberIds = distinctNewIds,
            group = updatedGroupDto
        });

        return updatedGroupDto;
    }

    public async Task<bool> RemoveMemberAsync(string requesterUserId, string groupId, string memberId)
    {
        var group = await _conversationRepository.GetByIdAsync(groupId);
        if (group == null || group.ConversationType != "Group")
        {
            throw new KeyNotFoundException("Group conversation not found.");
        }

        var isAdmin = group.AdminIds.Contains(requesterUserId);
        var isSelfLeaving = requesterUserId == memberId;

        if (!isAdmin && !isSelfLeaving)
        {
            throw new UnauthorizedAccessException("Only group admins can remove other members.");
        }

        var previousMembers = group.MemberIds.ToList();
        var removed = await _conversationRepository.RemoveMemberFromGroupAsync(groupId, memberId);

        if (removed)
        {
            await _hubContext.Clients.Users(previousMembers).SendAsync("GroupMemberRemoved", new
            {
                groupId,
                removedMemberId = memberId,
                removedBy = requesterUserId
            });
        }

        return removed;
    }

    public async Task<GroupDTO> RemoveMembersAsync(string requesterUserId, string groupId, List<string> memberIds)
    {
        var group = await _conversationRepository.GetByIdAsync(groupId);
        if (group == null || group.ConversationType != "Group")
        {
            throw new KeyNotFoundException("Group conversation not found.");
        }

        if (!group.AdminIds.Contains(requesterUserId))
        {
            throw new UnauthorizedAccessException("Only group admins can remove multiple members.");
        }

        var validIdsToRemove = memberIds.Where(id => group.MemberIds.Contains(id)).Distinct().ToList();
        var previousMembers = group.MemberIds.ToList();

        if (validIdsToRemove.Count > 0)
        {
            await _conversationRepository.RemoveMembersFromGroupAsync(groupId, validIdsToRemove);
            group.MemberIds.RemoveAll(id => validIdsToRemove.Contains(id));
            group.AdminIds.RemoveAll(id => validIdsToRemove.Contains(id));
        }

        var remainingUsers = await _userRepository.GetByIdsAsync(group.MemberIds);
        var membersDto = remainingUsers.Select(MapUserDto).ToList();

        var updatedGroupDto = new GroupDTO
        {
            Id = group.Id ?? string.Empty,
            GroupTitle = group.GroupTitle ?? string.Empty,
            GroupPictureUrl = group.GroupPictureUrl ?? string.Empty,
            AdminIds = group.AdminIds,
            MemberIds = group.MemberIds,
            Members = membersDto,
            LastMessage = group.LastMessage,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt
        };

        await _hubContext.Clients.Users(previousMembers).SendAsync("GroupMembersRemoved", new
        {
            groupId,
            removedMemberIds = validIdsToRemove,
            removedBy = requesterUserId,
            group = updatedGroupDto
        });

        return updatedGroupDto;
    }

    public async Task<bool> LeaveGroupAsync(string userId, string groupId)
    {
        var group = await _conversationRepository.GetByIdAsync(groupId);
        if (group == null || group.ConversationType != "Group")
        {
            throw new KeyNotFoundException("Group conversation not found.");
        }

        if (!group.MemberIds.Contains(userId))
        {
            throw new ArgumentException("You are not a member of this group.");
        }

        var previousMembers = group.MemberIds.ToList();
        var success = await _conversationRepository.RemoveMemberFromGroupAsync(groupId, userId);

        if (success)
        {
            await _hubContext.Clients.Users(previousMembers).SendAsync("GroupMemberLeft", new
            {
                groupId,
                leftUserId = userId
            });

            _logger.LogInformation("User {UserId} left group {GroupId}", userId, groupId);
        }

        return success;
    }

    public async Task<GroupDTO> PatchGroupAsync(string requesterUserId, string groupId, PatchGroupDTO dto)
    {
        var group = await _conversationRepository.GetByIdAsync(groupId);
        if (group == null || group.ConversationType != "Group")
        {
            throw new KeyNotFoundException("Group conversation not found.");
        }

        if (!group.AdminIds.Contains(requesterUserId) && !group.MemberIds.Contains(requesterUserId))
        {
            throw new UnauthorizedAccessException("You are not a member of this group.");
        }

        var newName = dto.GetResolvedGroupName();
        var newIcon = dto.GetResolvedGroupIcon();

        await _conversationRepository.UpdateGroupDetailsAsync(groupId, newName, newIcon);

        var updatedGroup = await _conversationRepository.GetByIdAsync(groupId) ?? group;
        var users = await _userRepository.GetByIdsAsync(updatedGroup.MemberIds);
        var membersDto = users.Select(MapUserDto).ToList();

        var updatedDto = new GroupDTO
        {
            Id = updatedGroup.Id ?? string.Empty,
            GroupTitle = updatedGroup.GroupTitle ?? string.Empty,
            GroupPictureUrl = updatedGroup.GroupPictureUrl ?? string.Empty,
            AdminIds = updatedGroup.AdminIds,
            MemberIds = updatedGroup.MemberIds,
            Members = membersDto,
            LastMessage = updatedGroup.LastMessage,
            CreatedAt = updatedGroup.CreatedAt,
            UpdatedAt = updatedGroup.UpdatedAt
        };

        // Notify all members of the group update in real-time
        await _hubContext.Clients.Users(updatedGroup.MemberIds).SendAsync("GroupUpdated", updatedDto);

        return updatedDto;
    }

    public async Task<bool> DeleteGroupAsync(string requesterUserId, string groupId)
    {
        var group = await _conversationRepository.GetByIdAsync(groupId);
        if (group == null || group.ConversationType != "Group")
        {
            throw new KeyNotFoundException("Group conversation not found.");
        }

        if (!group.AdminIds.Contains(requesterUserId))
        {
            throw new UnauthorizedAccessException("Only group admins can delete this group.");
        }

        var members = group.MemberIds.ToList();
        var deleted = await _conversationRepository.DeleteConversationAsync(groupId);

        if (deleted)
        {
            await _hubContext.Clients.Users(members).SendAsync("GroupDeleted", new { groupId });
        }

        return deleted;
    }

    public async Task<GroupDTO?> GetGroupDetailsAsync(string requesterUserId, string groupId)
    {
        var group = await _conversationRepository.GetByIdAsync(groupId);
        if (group == null || group.ConversationType != "Group")
        {
            return null;
        }

        if (!group.MemberIds.Contains(requesterUserId))
        {
            throw new UnauthorizedAccessException("You are not a member of this group.");
        }

        var users = await _userRepository.GetByIdsAsync(group.MemberIds);
        var membersDto = users.Select(MapUserDto).ToList();

        return new GroupDTO
        {
            Id = group.Id ?? string.Empty,
            GroupTitle = group.GroupTitle ?? string.Empty,
            GroupPictureUrl = group.GroupPictureUrl ?? string.Empty,
            AdminIds = group.AdminIds,
            MemberIds = group.MemberIds,
            Members = membersDto,
            LastMessage = group.LastMessage,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt
        };
    }

    private static UserDTO MapUserDto(User user)
    {
        return new UserDTO
        {
            Id = user.Id ?? string.Empty,
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            ProfilePictureUrl = user.ProfilePictureUrl,
            Bio = user.Bio,
            LastSeen = user.LastSeen,
            IsOnline = user.IsOnline,
            CreatedAt = user.CreatedAt
        };
    }
}
