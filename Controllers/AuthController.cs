using System.Security.Claims;
using Connectify.API.DTOs;
using Connectify.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connectify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new user account and returns a valid JWT Bearer token.
    /// </summary>
    /// <param name="registerDto">The registration payload.</param>
    /// <returns>AuthResponseDTO containing JWT token and user profile.</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterDTO registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _authService.RegisterAsync(registerDto);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration conflict: {Message}", ex.Message);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred during registration. Please try again." });
        }
    }

    /// <summary>
    /// Authenticates a user by email/username and password and returns a JWT Bearer token.
    /// </summary>
    /// <param name="loginDto">The login payload.</param>
    /// <returns>AuthResponseDTO containing JWT token and user profile.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDTO loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _authService.LoginAsync(loginDto);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during user login.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred during login. Please try again." });
        }
    }

    /// <summary>
    /// Retrieves the sanitized profile of the currently authenticated user.
    /// </summary>
    /// <returns>UserDTO containing the user's profile details.</returns>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Missing or invalid authorization claims." });
        }

        var profile = await _authService.GetCurrentUserAsync(userId);
        if (profile == null)
        {
            return NotFound(new { message = "User account not found." });
        }

        return Ok(profile);
    }
}
