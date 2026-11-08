using Connectify.API.DTOs;

namespace Connectify.API.Services;

public interface IUserService
{
    Task<IEnumerable<UserDTO>> GetAllUsersAsync(string currentUserId, int limit = 100, int skip = 0);
    Task<UserDTO?> GetUserByIdAsync(string id);
    Task<IEnumerable<UserDTO>> SearchUsersAsync(string query, string currentUserId);
    Task<UserDTO?> UpdateProfileAsync(string userId, UpdateProfileDTO dto);
}
