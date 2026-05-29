using System.Security.Claims;
using AspNetCore.Identity.Mongo.Model;
using FixIt.Models.Users;
using FixIt.Services.Authentication;
using FixIt.Services.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FixIt.Tests.Security;

/// <summary>
/// Verifies that <see cref="ApplicationUserClaimsPrincipalFactory"/> produces a
/// complete Identity principal: the core NameIdentifier plus the stable custom
/// claims (roles, display name, email) every sign-in path depends on.
/// </summary>
public class ClaimsPrincipalFactoryTests
{
    [Fact]
    public async Task CreateAsync_PopulatesNameIdentifier_Roles_DisplayName_AndEmail()
    {
        var user = new ApplicationUser
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            UserName = "jane",
            Email = "jane@example.com",
            DisplayName = "Jane Citizen"
        };

        var userManager = BuildUserManagerMock(user, roles: new[] { RoleNames.Admin, RoleNames.User });
        var roleManager = BuildRoleManagerMock();
        var options = Options.Create(new IdentityOptions());

        var factory = new ApplicationUserClaimsPrincipalFactory(userManager.Object, roleManager.Object, options);

        var principal = await factory.CreateAsync(user);
        var identity = principal.Identity as ClaimsIdentity;

        Assert.NotNull(identity);
        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.IsInRole(RoleNames.Admin));
        Assert.True(principal.IsInRole(RoleNames.User));
        Assert.Equal("Jane Citizen", principal.FindFirstValue(CustomClaimTypes.DisplayName));
        Assert.Equal("jane@example.com", principal.FindFirstValue(ClaimTypes.Email));
    }

    [Fact]
    public async Task CreateAsync_DoesNotDuplicateClaims_OnRepeatedCalls()
    {
        var user = new ApplicationUser
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            UserName = "sam",
            Email = "sam@example.com",
            DisplayName = "Sam"
        };

        var userManager = BuildUserManagerMock(user, roles: new[] { RoleNames.User });
        var roleManager = BuildRoleManagerMock();
        var options = Options.Create(new IdentityOptions());

        var factory = new ApplicationUserClaimsPrincipalFactory(userManager.Object, roleManager.Object, options);

        var principal = await factory.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        Assert.Single(identity.FindAll(CustomClaimTypes.DisplayName));
        Assert.Single(identity.FindAll(ClaimTypes.Email));
        Assert.Single(identity.FindAll(c => c.Type == ClaimTypes.Role && c.Value == RoleNames.User));
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock(ApplicationUser user, string[] roles)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var manager = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        // Keep the base factory on a minimal, deterministic path: it only adds the
        // user id + user name; our override adds roles, display name and email.
        manager.Setup(m => m.SupportsUserSecurityStamp).Returns(false);
        manager.Setup(m => m.SupportsUserClaim).Returns(false);
        manager.Setup(m => m.SupportsUserRole).Returns(false);
        manager.Setup(m => m.SupportsUserEmail).Returns(false);
        manager.Setup(m => m.GetUserIdAsync(user)).ReturnsAsync(user.Id.ToString());
        manager.Setup(m => m.GetUserNameAsync(user)).ReturnsAsync(user.UserName);
        manager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(roles.ToList());

        return manager;
    }

    private static Mock<RoleManager<MongoRole>> BuildRoleManagerMock()
    {
        var store = new Mock<IRoleStore<MongoRole>>();
        return new Mock<RoleManager<MongoRole>>(
            store.Object,
            new List<IRoleValidator<MongoRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<ILogger<RoleManager<MongoRole>>>().Object);
    }
}
