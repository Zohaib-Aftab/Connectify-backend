using Connectify.API.DTOs;
using Connectify.API.Models;
using Connectify.API.Repositories;

namespace Connectify.API.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDTO>> GetAllUsersAsync(string currentUserId, int limit = 100, int skip = 0)
    {
        var users = await _userRepository.GetAllAsync(limit, skip);

        return users
            .Where(u => u.Id != currentUserId)
            .Select(MapToUserDto)
            .ToList();
    }

    public async Task<UserDTO?> GetUserByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var user = await _userRepository.GetByIdAsync(id);
        return user == null ? null : MapToUserDto(user);
    }

    public async Task<IEnumerable<UserDTO>> SearchUsersAsync(string query, string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var users = await _userRepository.SearchUsersAsync(query);

        return users
            .Where(u => u.Id != currentUserId)
            .Select(MapToUserDto)
            .ToList();
    }

    public async Task<UserDTO?> UpdateProfileAsync(string userId, UpdateProfileDTO dto)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return null;

        var displayName = dto.GetDisplayName();
        await _userRepository.UpdateProfileAsync(userId, displayName, dto.Bio, dto.ProfilePictureUrl);

        var updatedUser = await _userRepository.GetByIdAsync(userId);
        _logger.LogInformation("Profile updated successfully for user {UserId}", userId);
        return updatedUser == null ? null : MapToUserDto(updatedUser);
    }

    private static UserDTO MapToUserDto(User user)
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
