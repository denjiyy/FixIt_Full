using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FixIt.Tests.Integration;

/// <summary>
/// Locks in the fix where browser (cookie) users got 401 on every
/// cookie-authenticated /api/* call. [ApiAuthorize] previously authenticated
/// against the generic "Cookies" scheme, but SignInManager signs users into
/// IdentityConstants.ApplicationScheme — a different cookie that nothing else
/// reads. The mismatch meant web voting/commenting/likes silently 401'd even
/// for signed-in users. ApiAuthorize now lists the application scheme.
///
/// This exercises the real path: cookie login via the Razor login page →
/// authenticated GET against an [ApiAuthorize] endpoint.
/// </summary>
public class CookieApiAuthIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public CookieApiAuthIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private void RequireDocker() => Skip.IfNot(_fixture.IsAvailable,
        $"Docker testcontainer unavailable. {_fixture.UnavailabilityReason ?? "(no reason captured)"}");

    [SkippableFact]
    public async Task CookieAuthenticatedUser_CanCallAuthenticatedApiEndpoint()
    {
        RequireDocker();

        var (email, password) = await _fixture.ProvisionRegularUserAsync(
            email: $"cookie-{Guid.NewGuid():N}@fixit.test");

        // HandleCookies (default true) keeps the antiforgery + auth cookies across
        // requests; AllowAutoRedirect=false so a successful login surfaces as 302.
        var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        // 1. Fetch the login page to mint an antiforgery token (and its cookie).
        var loginPage = await client.GetAsync("/Identity/Account/Login");
        loginPage.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());

        // 2. Post credentials → establishes the Identity.Application cookie.
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = token,
        });
        var loginResponse = await client.PostAsync("/Identity/Account/Login", form);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // 3. The regression: a cookie-authenticated [ApiAuthorize] endpoint must
        //    now return 200, not 401.
        var apiResponse = await client.GetAsync("/api/auth/user");
        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"");
        Assert.True(match.Success, "Could not find an antiforgery token on the login page.");
        return match.Groups[1].Value;
    }
}
