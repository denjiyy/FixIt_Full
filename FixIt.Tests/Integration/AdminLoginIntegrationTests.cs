using System.IdentityModel.Tokens.Jwt;
using FixIt.Services.Constants;
using Xunit;

namespace FixIt.Tests.Integration;

/// <summary>
/// Locks in the regression fix where admin users were created with only the
/// ApplicationUser.Role enum set but were never assigned the Identity Admin role.
/// The bug manifested as: admins could pass login, but every /admin/* policy
/// gate (which calls RequireRole) saw no role claim and returned 403.
///
/// This test exercises the chain that broke previously:
///   bootstrap admin → /api/auth/login → JWT includes the Admin role claim.
///
/// We assert the JWT contents directly rather than hitting an authorized
/// endpoint because the API controllers currently use the default (Cookie) auth
/// scheme, which silently ignores Bearer tokens for authorization decisions.
/// Wiring proper Bearer auth for /api/* is a Phase 3 item; the regression we
/// care about here is upstream of it.
/// </summary>
public class AdminLoginIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminLoginIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private void RequireDocker() => Skip.IfNot(_fixture.IsAvailable,
        $"Docker testcontainer unavailable. {_fixture.UnavailabilityReason ?? "(no reason captured)"}");

    [SkippableFact]
    public async Task BootstrappedAdmin_LoginsIssueJwtWithAdminRoleClaim()
    {
        RequireDocker();

        var (email, password) = await _fixture.ProvisionAdminAsync(
            email: $"admin-{Guid.NewGuid():N}@fixit.test");

        var (accessToken, _) = await _fixture.LoginAndGetClientAsync(email, password);

        var claims = ReadJwtClaims(accessToken);
        var roleClaims = claims
            .Where(c => c.Type == "role" || c.Type == "roles"
                || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();

        Assert.Contains(RoleNames.Admin, roleClaims);
    }

    [SkippableFact]
    public async Task RegularUser_LoginIssueJwtWithoutAdminRoleClaim()
    {
        RequireDocker();

        var (email, password) = await _fixture.ProvisionRegularUserAsync(
            email: $"user-{Guid.NewGuid():N}@fixit.test");

        var (accessToken, _) = await _fixture.LoginAndGetClientAsync(email, password);

        var claims = ReadJwtClaims(accessToken);
        var roleClaims = claims
            .Where(c => c.Type == "role" || c.Type == "roles"
                || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();

        Assert.DoesNotContain(RoleNames.Admin, roleClaims);
    }

    private static IReadOnlyList<System.Security.Claims.Claim> ReadJwtClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        return jwt.Claims.ToList();
    }
}
