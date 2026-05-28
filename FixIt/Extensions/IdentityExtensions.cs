using AspNetCore.Identity.Mongo;
using AspNetCore.Identity.Mongo.Model;
using FixIt.Models.Users;
using FixIt.Services.Authentication;
using FixIt.Services.Constants;
using Microsoft.AspNetCore.Identity;

namespace FixIt.Extensions;

public static class IdentityExtensions
{
    public static IServiceCollection AddIdentityWithMongo(
        this IServiceCollection services,
        string mongoConnectionString)
    {
        services.AddIdentityMongoDbProvider<ApplicationUser, MongoRole>(identity =>
        {
            identity.Password.RequireDigit = true;
            identity.Password.RequiredLength = 8;
            identity.Password.RequireNonAlphanumeric = false;
            identity.Password.RequireUppercase = true;
            identity.Password.RequireLowercase = true;
            identity.User.RequireUniqueEmail = true;

            identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            identity.Lockout.MaxFailedAccessAttempts = 5;
            identity.Lockout.AllowedForNewUsers = true;
        },
        mongo =>
        {
            mongo.ConnectionString = mongoConnectionString;
        });

        // Custom claims principal factory so User.IsInRole() resolves the user's
        // single Role enum through the same channel as cookie-based auth.
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();

        services.AddAuthorization(options =>
        {
            // Policies intentionally do not specify AuthenticationSchemes.
            // PolicyEvaluator.HandleForbiddenAsync iterates ALL listed schemes
            // and the last response wins — if Cookies is listed, its 302
            // redirect clobbers Bearer's 403 even for API requests. Instead,
            // scheme selection happens up the stack via the MultiScheme
            // PolicyScheme registered in AuthExtensions: API paths forward to
            // Bearer, everything else forwards to Cookies. The role check
            // below then runs on whichever identity was attached.
            options.AddPolicy(PolicyNames.AdminArea, policy =>
                policy.RequireRole(RoleNames.Admin, RoleNames.Moderator));
            options.AddPolicy(PolicyNames.AdminOnly, policy =>
                policy.RequireRole(RoleNames.Admin));
        });

        return services;
    }
}
