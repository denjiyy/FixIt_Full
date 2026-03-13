namespace FixIt.ViewModels.Auth;

/// <summary>
/// Response containing JWT tokens for authentication
/// Used for mobile and API clients
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// JWT access token for authenticating API requests
    /// Include in Authorization header as "Bearer {access_token}"
    /// </summary>
    public string AccessToken { get; set; } = null!;

    /// <summary>
    /// JWT refresh token for obtaining a new access token
    /// Use with RefreshToken endpoint when access token expires
    /// </summary>
    public string RefreshToken { get; set; } = null!;

    /// <summary>
    /// Token type (always "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// How many seconds until the access token expires
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// User information associated with the token
    /// </summary>
    public UserTokenInfo User { get; set; } = null!;
}

/// <summary>
/// User information included in authentication responses
/// </summary>
public class UserTokenInfo
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// User's display name
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// User's roles (e.g., "User", "Moderator", "Admin")
    /// </summary>
    public IEnumerable<string> Roles { get; set; } = [];

    /// <summary>
    /// User's reputation score
    /// </summary>
    public int ReputationScore { get; set; }

    /// <summary>
    /// User's trust level
    /// </summary>
    public int TrustLevel { get; set; }

    /// <summary>
    /// Whether user is a verified official
    /// </summary>
    public bool IsVerifiedOfficial { get; set; }

    /// <summary>
    /// User's official title (if verified official)
    /// </summary>
    public string? OfficialTitle { get; set; }

    /// <summary>
    /// User's official department (if verified official)
    /// </summary>
    public string? OfficialDepartment { get; set; }
}

/// <summary>
/// Request to refresh an access token
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// The refresh token received from login
    /// </summary>
    public string RefreshToken { get; set; } = null!;
}

/// <summary>
/// Response for token refresh request
/// Contains a new access token
/// </summary>
public class RefreshTokenResponse
{
    /// <summary>
    /// New JWT access token
    /// </summary>
    public string AccessToken { get; set; } = null!;

    /// <summary>
    /// Token type (always "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// How many seconds until the new access token expires
    /// </summary>
    public int ExpiresIn { get; set; }
}
