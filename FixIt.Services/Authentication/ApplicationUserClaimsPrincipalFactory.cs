using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using FixIt.Models.Users;

namespace FixIt.Services.Authentication;

/// <summary>
/// Custom claims principal factory that adds the user's role as a claim.
/// This allows User.IsInRole() to work correctly in Razor pages and controllers.
/// </summary>
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = principal.Identity as ClaimsIdentity;

        if (identity != null)
        {
            // Add the user's role as a role claim so IsInRole() works
            identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        }

        return principal;
    }
}
