using Connectify.API.DTOs;
using Connectify.API.Models;

namespace Connectify.API.Services;

public interface IAuthService
{
    Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto);
    Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
    Task<UserDTO?> GetCurrentUserAsync(string userId);
    string GenerateJwtToken(User user, out DateTime expiresAt);
    UserDTO MapToUserDto(User user);
}
