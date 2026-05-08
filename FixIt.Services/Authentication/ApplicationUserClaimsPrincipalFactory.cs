using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using FixIt.Models.Users;
using AspNetCore.Identity.Mongo.Model;  // <-- add this

namespace FixIt.Services.Authentication;

public class ApplicationUserClaimsPrincipalFactory 
    : UserClaimsPrincipalFactory<ApplicationUser, MongoRole>  // <-- change to MongoRole
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<MongoRole> roleManager,                    // <-- use MongoRole
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
        _userManager = userManager;
    }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = principal.Identity as ClaimsIdentity;

        if (identity != null)
        {
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return principal;
    }
}