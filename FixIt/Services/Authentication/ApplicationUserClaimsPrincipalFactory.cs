using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using FixIt.Models.Users;

namespace FixIt.Services.Authentication;

/// <summary>
/// Custom claims principal factory that adds the user's role as a claim
/// This allows User.IsInRole() to work correctly in Razor pages and controllers
/// </summary>
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    /// <summary>
    /// Override to add the user's role as a claim
    /// </summary>
    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        
        if (principal.Identity is ClaimsIdentity claimsIdentity)
        {
            // Add the user's role as a claim
            // This makes User.IsInRole() work in Razor pages and controllers
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        }

        return principal;
    }
}
