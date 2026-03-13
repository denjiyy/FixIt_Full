using FixIt.Models.Users;

namespace FixIt.Services.Authentication;

/// <summary>
/// Interface for JWT token generation and validation
/// Supports both access tokens and refresh tokens for mobile clients
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user
    /// </summary>
    /// <param name="user">The user to generate token for</param>
    /// <param name="roles">User roles to include in token</param>
    /// <returns>JWT access token string</returns>
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>
    /// Generates a JWT refresh token for the specified user
    /// </summary>
    /// <param name="user">The user to generate token for</param>
    /// <returns>JWT refresh token string</returns>
    string GenerateRefreshToken(ApplicationUser user);

    /// <summary>
    /// Validates a JWT token and returns the principal if valid
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    System.Security.Claims.ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// Gets the expiration time in minutes for access tokens
    /// </summary>
    int AccessTokenExpirationMinutes { get; }

    /// <summary>
    /// Gets the expiration time in days for refresh tokens
    /// </summary>
    int RefreshTokenExpirationDays { get; }
}
