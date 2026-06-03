using System.Net;
using System.Text;
using FixIt.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FixIt.Tests.Integration;

/// <summary>
/// Verifies the email-confirmation mechanism end to end: a token minted by
/// GenerateEmailConfirmationTokenAsync (which depends on AddDefaultTokenProviders
/// being wired) is accepted by the ConfirmEmail page and flips the account to
/// confirmed. Also covers the tampered-token rejection path.
/// </summary>
public class EmailConfirmationIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public EmailConfirmationIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private void RequireDocker() => Skip.IfNot(_fixture.IsAvailable,
        $"Docker testcontainer unavailable. {_fixture.UnavailabilityReason ?? "(no reason captured)"}");

    [SkippableFact]
    public async Task ConfirmEmail_WithValidToken_ConfirmsAccount()
    {
        RequireDocker();

        var email = $"confirm-{Guid.NewGuid():N}@fixit.test";
        var (userId, code) = await CreateUnconfirmedUserAndTokenAsync(email);

        var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync($"/Identity/Account/ConfirmEmail?userId={userId}&code={code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await IsConfirmedAsync(userId), "Account should be confirmed after a valid token.");
    }

    [SkippableFact]
    public async Task ConfirmEmail_WithTamperedToken_DoesNotConfirm()
    {
        RequireDocker();

        var email = $"tamper-{Guid.NewGuid():N}@fixit.test";
        var (userId, _) = await CreateUnconfirmedUserAndTokenAsync(email);

        var bogus = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("not-a-real-token"));
        var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync($"/Identity/Account/ConfirmEmail?userId={userId}&code={bogus}");

        // The page renders a failure state (200) but the account stays unconfirmed.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await IsConfirmedAsync(userId), "Account must not confirm on a tampered token.");
    }

    private async Task<(string UserId, string Code)> CreateUnconfirmedUserAndTokenAsync(string email)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            DisplayName = "Confirm Me",
            HasPasswordAuth = true,
        };
        var created = await userManager.CreateAsync(user, "TestUserPass1");
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return (user.Id.ToString(), code);
    }

    private async Task<bool> IsConfirmedAsync(string userId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        Assert.NotNull(user);
        return await userManager.IsEmailConfirmedAsync(user!);
    }
}
