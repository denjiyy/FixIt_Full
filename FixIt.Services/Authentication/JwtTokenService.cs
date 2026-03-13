using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FixIt.Models.Users;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.Authentication;

/// <summary>
/// JWT token service for generating and validating JWT tokens
/// Used for mobile and API clients that cannot use cookies
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenService> _logger;

    public int AccessTokenExpirationMinutes { get; private set; }
    public int RefreshTokenExpirationDays { get; private set; }

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Read expiration times from config, with defaults
        AccessTokenExpirationMinutes = _configuration.GetValue("Jwt:AccessTokenExpirationMinutes", 30);
        RefreshTokenExpirationDays = _configuration.GetValue("Jwt:RefreshTokenExpirationDays", 7);
    }

    /// <summary>
    /// Generates a JWT access token with user claims
    /// </summary>
    public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT:SecretKey is not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "FixIt";
        var audience = _configuration["Jwt:Audience"] ?? "FixItClients";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.UserName ?? ""),
            new Claim("DisplayName", user.DisplayName ?? ""),
            new Claim("ReputationScore", user.ReputationScore.ToString()),
            new Claim("TrustLevel", user.TrustLevel.ToString()),
            new Claim("IsVerifiedOfficial", user.IsVerifiedOfficial.ToString()),
            new Claim("IsBanned", user.IsBanned.ToString()),
            new Claim("IsRestricted", user.IsRestricted.ToString()),
        };

        // Add roles as claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add official title if verified official
        if (user.IsVerifiedOfficial && !string.IsNullOrEmpty(user.OfficialTitle))
        {
            claims.Add(new Claim("OfficialTitle", user.OfficialTitle));
            claims.Add(new Claim("OfficialDepartment", user.OfficialDepartment ?? ""));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenExpirationMinutes),
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Generates a JWT refresh token
    /// Refresh tokens are longer-lived and can be exchanged for new access tokens
    /// </summary>
    public string GenerateRefreshToken(ApplicationUser user)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT:SecretKey is not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "FixIt";
        var audience = _configuration["Jwt:Audience"] ?? "FixItClients";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("TokenType", "refresh"),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Validates a JWT token and returns the claims principal
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var secretKey = _configuration["Jwt:SecretKey"]
                ?? throw new InvalidOperationException("JWT:SecretKey is not configured");
            var issuer = _configuration["Jwt:Issuer"] ?? "FixIt";
            var audience = _configuration["Jwt:Audience"] ?? "FixItClients";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(0)
            }, out SecurityToken validatedToken);

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Token validation failed: {ex.Message}");
            return null;
        }
    }
}
