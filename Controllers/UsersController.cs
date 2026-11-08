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
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>
    /// Fetches the authenticated caller's own profile.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Missing or invalid authorization claims." });
        }

        var profile = await _userService.GetUserByIdAsync(userId);
        if (profile == null)
        {
            return NotFound(new { message = "User profile not found." });
        }

        return Ok(profile);
    }

    /// <summary>
    /// Updates caller profile fields (Name, Bio, ProfilePictureUrl) dynamically.
    /// </summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Missing or invalid authorization claims." });
        }

        var updatedProfile = await _userService.UpdateProfileAsync(userId, dto);
        if (updatedProfile == null)
        {
            return NotFound(new { message = "User profile not found." });
        }

        return Ok(updatedProfile);
    }

    /// <summary>
    /// Performs text query searching inside MongoDB for usernames or names.
    /// </summary>
    /// <param name="q">Search query string.</param>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<UserDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchUsers([FromQuery] string q)
    {
        var users = await _userService.SearchUsersAsync(q, CurrentUserId);
        return Ok(users);
    }

    /// <summary>
    /// Fetches all registered user profiles available for conversation, excluding the requester.
    /// </summary>
    /// <param name="limit">Max number of profiles to fetch.</param>
    /// <param name="skip">Pagination offset.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers([FromQuery] int limit = 100, [FromQuery] int skip = 0)
    {
        var users = await _userService.GetAllUsersAsync(CurrentUserId, limit, skip);
        return Ok(users);
    }

    /// <summary>
    /// Retrieves a specific user profile by user identifier.
    /// </summary>
    /// <param name="id">Target user ID.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById([FromRoute] string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"User with ID '{id}' was not found." });
        }

        return Ok(user);
    }
}
