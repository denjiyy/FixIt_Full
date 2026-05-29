using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using FixIt.Models.Users;
using FixIt.Services.Constants;
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
        // The base factory establishes the core Identity claims (NameIdentifier,
        // Name, SecurityStamp). We enrich it with the stable custom claims that
        // the web UI and authorization rely on, so that every sign-in path
        // (password and external) yields an identical, complete principal.
        var principal = await base.CreateAsync(user);

        if (principal.Identity is ClaimsIdentity identity)
        {
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                if (!identity.HasClaim(ClaimTypes.Role, role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }

            if (!string.IsNullOrWhiteSpace(user.DisplayName) &&
                !identity.HasClaim(c => c.Type == CustomClaimTypes.DisplayName))
            {
                identity.AddClaim(new Claim(CustomClaimTypes.DisplayName, user.DisplayName));
            }

            if (!string.IsNullOrWhiteSpace(user.Email) &&
                !identity.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
            }
        }

        return principal;
    }
}