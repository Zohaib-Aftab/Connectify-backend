using System.Security.Claims;
using Connectify.API.DTOs;
using Connectify.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connectify.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GroupsController : ControllerBase
{
    private readonly IGroupService _groupService;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(IGroupService groupService, ILogger<GroupsController> logger)
    {
        _groupService = groupService;
        _logger = logger;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>
    /// Creates a new group conversation with Custom Group Name, Group Icon avatar, Member IDs list, and Admins control arrays.
    /// </summary>
    /// <param name="dto">Group creation payload.</param>
    [HttpPost]
    [ProducesResponseType(typeof(GroupDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var group = await _groupService.CreateGroupAsync(CurrentUserId, dto);
            return StatusCode(StatusCodes.Status201Created, group);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create group.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the group." });
        }
    }

    /// <summary>
    /// Fetches details and member roster of a specific group conversation.
    /// </summary>
    /// <param name="id">Group ID.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GroupDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroupDetails([FromRoute] string id)
    {
        try
        {
            var group = await _groupService.GetGroupDetailsAsync(CurrentUserId, id);
            if (group == null)
            {
                return NotFound(new { message = "Group not found." });
            }

            return Ok(group);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching group details {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to load group details." });
        }
    }

    /// <summary>
    /// Adds one or more members to an existing group conversation.
    /// </summary>
    /// <param name="id">Group ID.</param>
    /// <param name="dto">List of user IDs to add.</param>
    [HttpPost("{id}/members")]
    [ProducesResponseType(typeof(GroupDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMembers([FromRoute] string id, [FromBody] AddGroupMembersDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updatedGroup = await _groupService.AddMembersAsync(CurrentUserId, id, dto.MemberIds);
            return Ok(updatedGroup);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding members to group {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to add members to the group." });
        }
    }

    /// <summary>
    /// Removes a participant from the group.
    /// </summary>
    /// <param name="id">Group ID.</param>
    /// <param name="memberId">User ID of the member to remove.</param>
    [HttpDelete("{id}/members/{memberId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember([FromRoute] string id, [FromRoute] string memberId)
    {
        try
        {
            var success = await _groupService.RemoveMemberAsync(CurrentUserId, id, memberId);
            if (!success)
            {
                return NotFound(new { message = "Member was not found in this group." });
            }

            return Ok(new { message = "Member successfully removed." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from group {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to remove member." });
        }
    }

    /// <summary>
    /// Removes multiple member IDs from the group conversation.
    /// </summary>
    /// <param name="id">Group ID.</param>
    /// <param name="dto">Payload with member IDs to remove.</param>
    [HttpPost("{id}/members/remove")]
    [ProducesResponseType(typeof(GroupDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMembersBatch([FromRoute] string id, [FromBody] RemoveGroupMembersDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updatedGroup = await _groupService.RemoveMembersAsync(CurrentUserId, id, dto.MemberIds);
            return Ok(updatedGroup);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing members batch from group {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to remove members." });
        }
    }

    /// <summary>
    /// Allows the authenticated user to leave the group conversation.
    /// </summary>
    /// <param name="id">Group ID.</param>
    [HttpPost("{id}/leave")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LeaveGroup([FromRoute] string id)
    {
        try
        {
            var success = await _groupService.LeaveGroupAsync(CurrentUserId, id);
            if (!success)
            {
                return NotFound(new { message = "Group not found or already left." });
            }

            return Ok(new { message = "You have left the group." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving group {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to leave group." });
        }
    }

    /// <summary>
    /// Patches dynamic group state such as Group Name or Group Icon picture URL.
    /// </summary>
    /// <param name="id">Group ID.</param>
    /// <param name="dto">Patch payload with new name and/or icon.</param>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(GroupDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchGroup([FromRoute] string id, [FromBody] PatchGroupDTO dto)
    {
        try
        {
            var updatedGroup = await _groupService.PatchGroupAsync(CurrentUserId, id, dto);
            return Ok(updatedGroup);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error patching group {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to update group details." });
        }
    }

    /// <summary>
    /// Deletes a group conversation (admin only).
    /// </summary>
    /// <param name="id">Group ID.</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGroup([FromRoute] string id)
    {
        try
        {
            var success = await _groupService.DeleteGroupAsync(CurrentUserId, id);
            if (!success)
            {
                return NotFound(new { message = "Group not found." });
            }

            return Ok(new { message = "Group deleted successfully." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting group {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to delete group." });
        }
    }
}
