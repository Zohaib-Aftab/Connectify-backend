using System.Security.Claims;
using Connectify.API.DTOs;
using Connectify.API.Repositories;
using Connectify.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connectify.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IConversationRepository _conversationRepository;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageService messageService,
        IConversationRepository conversationRepository,
        ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _conversationRepository = conversationRepository;
        _logger = logger;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>
    /// Retrieves paginated historical message logs for a specified conversation.
    /// Strictly verifies that the authenticated User ID exactly matches one of the MemberIds.
    /// </summary>
    /// <param name="chatId">Conversation ID.</param>
    /// <param name="limit">Page size (defaults to 50).</param>
    /// <param name="skip">Pagination offset.</param>
    [HttpGet("{chatId}")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<MessageDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationMessages(
        [FromRoute] string chatId,
        [FromQuery] int limit = 50,
        [FromQuery] int skip = 0)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized(new { message = "User identity could not be verified from token." });
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest(new { message = "Conversation ID is required." });
        }

        // Security Validation: Ensure the conversation exists and caller is an active member
        var conversation = await _conversationRepository.GetByIdAsync(chatId);
        if (conversation == null)
        {
            return NotFound(new { message = "Conversation not found." });
        }

        if (!conversation.MemberIds.Contains(CurrentUserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Access denied: You are not authorized to view messages in this conversation."
            });
        }

        try
        {
            var messages = await _messageService.GetConversationMessagesAsync(chatId, CurrentUserId, limit, skip);
            return Ok(messages);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching messages for conversation {ChatId}", chatId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to load chat history." });
        }
    }

    /// <summary>
    /// Sends a new text message or attachment and commits it to the NoSQL database.
    /// Verifies conversation membership before writing.
    /// </summary>
    /// <param name="dto">Message payload containing conversation or recipient info, text content, and optional attachment.</param>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDTO dto)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized(new { message = "User identity could not be verified from token." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string resolvedConversationId = dto.GetResolvedConversationId();
        if (!string.IsNullOrEmpty(resolvedConversationId))
        {
            var conversation = await _conversationRepository.GetByIdAsync(resolvedConversationId);
            if (conversation != null && !conversation.MemberIds.Contains(CurrentUserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Access denied: You are not authorized to send messages to this conversation."
                });
            }
        }

        try
        {
            var createdMessage = await _messageService.SendMessageAsync(CurrentUserId, dto);
            return StatusCode(StatusCodes.Status201Created, createdMessage);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to send message. Please try again." });
        }
    }
}
