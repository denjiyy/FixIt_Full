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
        string mongoConnectionString,
        bool requireConfirmedEmail = false)
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

            // When enabled, PasswordSignInAsync returns IsNotAllowed for unconfirmed
            // accounts and the API login gate rejects them. The confirmation
            // mechanism (email link + ConfirmEmail page + resend) is always wired
            // regardless of this flag; the flag only controls enforcement at login.
            identity.SignIn.RequireConfirmedEmail = requireConfirmedEmail;
        },
        mongo =>
        {
            mongo.ConnectionString = mongoConnectionString;
        })
        // Registers the data-protection token providers backing
        // GenerateEmailConfirmationTokenAsync / GeneratePasswordResetTokenAsync.
        .AddDefaultTokenProviders();

        // Custom claims principal factory so User.IsInRole() resolves the user's
        // single Role enum through the same channel as cookie-based auth.
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();

        services.AddAuthorization(options =>
        {
            // These policies don't pin AuthenticationSchemes themselves. Web Razor
            // pages authorize against Identity's default application cookie; API
            // endpoints opt in to both cookie + bearer via [ApiAuthorize], which
            // sets AuthenticationSchemes = IdentityConstants.ApplicationScheme +
            // Bearer. The application cookie is configured (see
            // AuthExtensions.ConfigureApplicationCookie) to return 401/403 for /api
            // paths instead of a 302 login redirect, so a cookie challenge never
            // clobbers bearer's status code on an API request. The role check below
            // then runs on whichever identity was attached.
            options.AddPolicy(PolicyNames.AdminArea, policy =>
                policy.RequireRole(RoleNames.Admin, RoleNames.Moderator));
            options.AddPolicy(PolicyNames.AdminOnly, policy =>
                policy.RequireRole(RoleNames.Admin));
        });

        return services;
    }
}
