using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Connectify.API.DTOs;
using Connectify.API.Models;
using Connectify.API.Repositories;
using Connectify.API.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Connectify.API.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IOptions<JwtSettings> jwtOptions,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _jwtSettings = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto)
    {
        var normalizedEmail = registerDto.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(normalizedEmail))
        {
            throw new InvalidOperationException("An account with this email address already exists.");
        }

        var baseUsername = !string.IsNullOrWhiteSpace(registerDto.Username)
            ? registerDto.Username.Trim()
            : normalizedEmail.Split('@')[0].Replace(".", "_").Replace("+", "_");

        if (string.IsNullOrWhiteSpace(baseUsername) || baseUsername.Length < 3)
        {
            baseUsername = "user_" + Guid.NewGuid().ToString("N")[..6];
        }

        var trimmedUsername = baseUsername;

        if (string.IsNullOrWhiteSpace(registerDto.Username) && await _userRepository.ExistsByUsernameAsync(trimmedUsername))
        {
            trimmedUsername = $"{trimmedUsername}_{Random.Shared.Next(1000, 9999)}";
        }
        else if (await _userRepository.ExistsByUsernameAsync(trimmedUsername))
        {
            throw new InvalidOperationException("This username is already taken. Please choose another one.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password, workFactor: 11);

        var newUser = new User
        {
            FullName = registerDto.FullName.Trim(),
            Username = trimmedUsername,
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            ProfilePictureUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(trimmedUsername)}",
            Bio = "Hey there! I am using Connectify.",
            IsOnline = true,
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdUser = await _userRepository.CreateAsync(newUser);
        var token = GenerateJwtToken(createdUser, out var expiresAt);

        _logger.LogInformation("New user registered successfully: {Username} ({Email})", createdUser.Username, createdUser.Email);

        return new AuthResponseDTO
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = MapToUserDto(createdUser)
        };
    }

    public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
    {
        var identifier = loginDto.Email.Trim();
        var user = await _userRepository.GetByEmailOrUsernameAsync(identifier);

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for identifier: {Identifier}", identifier);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!string.IsNullOrEmpty(user.Id))
        {
            await _userRepository.UpdateOnlineStatusAsync(user.Id, true);
            user.IsOnline = true;
            user.LastSeen = DateTime.UtcNow;
        }

        var token = GenerateJwtToken(user, out var expiresAt);

        _logger.LogInformation("User logged in successfully: {Username}", user.Username);

        return new AuthResponseDTO
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = MapToUserDto(user)
        };
    }

    public async Task<UserDTO?> GetCurrentUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var user = await _userRepository.GetByIdAsync(userId);
        return user == null ? null : MapToUserDto(user);
    }

    public string GenerateJwtToken(User user, out DateTime expiresAt)
    {
        var secretKey = _jwtSettings.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
        {
            secretKey = "ConnectifySuperSecretDevelopmentEncryptionKeyWithMoreThan32Characters!";
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("fullName", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiryDays = _jwtSettings.ExpiryInDays > 0 ? _jwtSettings.ExpiryInDays : 7;
        expiresAt = DateTime.UtcNow.AddDays(expiryDays);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = credentials,
            Issuer = string.IsNullOrWhiteSpace(_jwtSettings.Issuer) ? "ConnectifyAPI" : _jwtSettings.Issuer,
            Audience = string.IsNullOrWhiteSpace(_jwtSettings.Audience) ? "ConnectifyClient" : _jwtSettings.Audience
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public UserDTO MapToUserDto(User user)
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
