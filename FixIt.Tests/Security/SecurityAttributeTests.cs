using System.Reflection;
using FixIt.Controllers;
using FixIt.Extensions;
using FixIt.Pages.Issues;
using FixIt.Services.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace FixIt.Tests.Security;

public class SecurityAttributeTests
{
    [Fact]
    public void SafetyController_ResolveHazard_IsAdminOnly()
    {
        var method = typeof(SafetyController).GetMethod(nameof(SafetyController.ResolveHazard));
        var authorize = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        // Migrated from inline [Authorize(Roles=...)] to a named policy so the
        // policy can specify both Bearer and Cookie schemes (otherwise mobile
        // clients with JWTs get silently 302'd to /Identity/Account/Login).
        Assert.Equal(PolicyNames.AdminOnly, authorize!.Policy);
    }

    [Fact]
    public void SafetyController_ToggleAnonymousReporting_RequiresAntiforgery()
    {
        var method = typeof(SafetyController).GetMethod(nameof(SafetyController.ToggleAnonymousReporting));
        // Endpoint is reachable via both cookie and bearer auth, so it uses
        // [ConditionalAntiforgery] (CSRF enforced for browsers, skipped for
        // bearer/mobile clients) in place of plain [ValidateAntiForgeryToken].
        var antiforgery = method?.GetCustomAttribute<ConditionalAntiforgeryAttribute>();

        Assert.NotNull(antiforgery);
    }

    [Fact]
    public void SafetyController_UpdateAlertPreferences_RequiresAntiforgery()
    {
        var method = typeof(SafetyController).GetMethod(nameof(SafetyController.UpdateAlertPreferences));
        var antiforgery = method?.GetCustomAttribute<ConditionalAntiforgeryAttribute>();

        Assert.NotNull(antiforgery);
    }

    [Fact]
    public void UsersController_UpdateProfileVisibility_RequiresAntiforgery()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.UpdateProfileVisibility));
        var antiforgery = method?.GetCustomAttribute<ConditionalAntiforgeryAttribute>();

        Assert.NotNull(antiforgery);
    }

    [Fact]
    public void AuthAndAnalysis_Controllers_HaveExpectedRateLimitPolicies()
    {
        var authPolicy = typeof(AuthController).GetCustomAttribute<EnableRateLimitingAttribute>();
        var analysisPolicy = typeof(AnalysisController).GetCustomAttribute<EnableRateLimitingAttribute>();
        var createIssuePolicy = typeof(CreateIssueModel).GetCustomAttribute<EnableRateLimitingAttribute>();
        var identityLoginPolicy = typeof(FixIt.Areas.Identity.Pages.Account.LoginModel).GetCustomAttribute<EnableRateLimitingAttribute>();
        var adminLoginPolicy = typeof(FixIt.Areas.Admin.Pages.LoginModel).GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(authPolicy);
        Assert.Equal(RateLimitPolicyNames.AuthStrict, authPolicy!.PolicyName);

        Assert.NotNull(analysisPolicy);
        Assert.Equal(RateLimitPolicyNames.Reporting, analysisPolicy!.PolicyName);

        Assert.NotNull(createIssuePolicy);
        Assert.Equal(RateLimitPolicyNames.Upload, createIssuePolicy!.PolicyName);

        Assert.NotNull(identityLoginPolicy);
        Assert.Equal(RateLimitPolicyNames.AuthStrict, identityLoginPolicy!.PolicyName);

        Assert.NotNull(adminLoginPolicy);
        Assert.Equal(RateLimitPolicyNames.AuthStrict, adminLoginPolicy!.PolicyName);
    }
}
