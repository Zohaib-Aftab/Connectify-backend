using Connectify.API.Models;

namespace Connectify.API.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<string> ids);
    Task<IEnumerable<User>> GetAllAsync(int limit = 100, int skip = 0);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailOrUsernameAsync(string identifier);
    Task<IEnumerable<User>> SearchByUsernameAsync(string query);
    Task<IEnumerable<User>> SearchUsersAsync(string query);
    Task<User> CreateAsync(User user);
    Task<bool> UpdateAsync(string id, User user);
    Task<bool> UpdateProfileAsync(string id, string? fullName, string? bio, string? profilePictureUrl);
    Task<bool> ExistsByEmailAsync(string email);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<bool> UpdateOnlineStatusAsync(string id, bool isOnline);
}
