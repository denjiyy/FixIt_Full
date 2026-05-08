using System.Security.Claims;
using FixIt.Models.Users;
using FixIt.Services.Authentication;
using FixIt.Services.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FixIt.Tests.Services;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "this_is_a_test_secret_key_for_jwt_validation_1234567890",
                ["Jwt:Issuer"] = "FixIt",
                ["Jwt:Audience"] = "FixItClients",
                ["Jwt:AccessTokenExpirationMinutes"] = "30",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            })
            .Build();

        return new JwtTokenService(config, Mock.Of<ILogger<JwtTokenService>>());
    }

    [Fact]
    public void GenerateAccessToken_ThenValidateToken_ReturnsPrincipalWithRole()
    {
        var service = CreateService();
        var userId = MongoDB.Bson.ObjectId.GenerateNewId();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = "User One"
        };

        var token = service.GenerateAccessToken(user, new[] { RoleNames.Admin });
        var principal = service.ValidateToken(token);

        Assert.NotNull(principal);
        Assert.Equal(userId.ToString(), principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Contains(principal.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == RoleNames.Admin);
        Assert.Equal("access", principal.FindFirstValue("TokenType"));
    }

    [Fact]
    public void GenerateRefreshToken_ThenValidateToken_HasRefreshTokenType()
    {
        var service = CreateService();
        var userId = MongoDB.Bson.ObjectId.GenerateNewId();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "refresh@example.com",
            DisplayName = "Refresh User",
            RefreshTokenVersion = 2
        };

        var refreshToken = service.GenerateRefreshToken(user);
        var principal = service.ValidateToken(refreshToken);

        Assert.NotNull(principal);
        Assert.Equal("refresh", principal!.FindFirstValue("TokenType"));
        Assert.Equal("2", principal.FindFirstValue("TokenVersion"));
    }
}
