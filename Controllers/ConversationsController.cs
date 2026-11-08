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
public class ConversationsController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(IMessageService messageService, ILogger<ConversationsController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>
    /// Checks if an active 1-to-1 conversation exists between the current user and the recipient.
    /// Creates a new conversation record if none exists, and returns the conversation details.
    /// </summary>
    /// <param name="dto">Payload containing the recipient's User ID.</param>
    [HttpPost("private")]
    [Authorize]
    [ProducesResponseType(typeof(ConversationDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrCreatePrivateConversation([FromBody] CreatePrivateConversationDTO dto)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized(new { message = "User identity could not be verified from token." });
        }

        var targetRecipientId = dto.GetTargetUserId();
        if (string.IsNullOrWhiteSpace(targetRecipientId))
        {
            return BadRequest(new { message = "Recipient user ID is required." });
        }

        try
        {
            var conversation = await _messageService.GetOrCreatePrivateConversationAsync(CurrentUserId, targetRecipientId);
            return Ok(new
            {
                conversationId = conversation.Id,
                id = conversation.Id,
                conversation
            });
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
            _logger.LogError(ex, "Error creating or retrieving private conversation.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to establish conversation session." });
        }
    }

    /// <summary>
    /// Returns the active session conversation list for the authenticated user with previews,
    /// recipient profile data, and unread counters.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ConversationDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserConversations()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized(new { message = "User identity could not be verified from token." });
        }

        try
        {
            var conversations = await _messageService.GetUserConversationsAsync(CurrentUserId);
            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations for user {UserId}", CurrentUserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to load conversation history." });
        }
    }

    /// <summary>
    /// Retrieves details and metadata for a specific conversation.
    /// Strictly verifies that the authenticated user is listed in MemberIds.
    /// </summary>
    /// <param name="id">Conversation ID.</param>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ConversationDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationById([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized(new { message = "User identity could not be verified from token." });
        }

        try
        {
            var conversation = await _messageService.GetConversationByIdAsync(id, CurrentUserId);
            if (conversation == null)
            {
                return NotFound(new { message = "Conversation not found." });
            }

            return Ok(conversation);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching conversation details for {ConversationId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to load conversation details." });
        }
    }
}
