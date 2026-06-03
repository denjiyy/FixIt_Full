using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FixIt.Extensions;

/// <summary>
/// [Authorize] variant for /api/* endpoints that accepts BOTH authentication
/// schemes:
///   - JWT bearer  → mobile / external API clients
///   - Cookie      → browser (Razor pages) calling the same JSON endpoints
///
/// The cookie scheme MUST be <see cref="IdentityConstants.ApplicationScheme"/> —
/// that is the cookie SignInManager actually writes on sign-in. A prior version
/// listed the generic "Cookies" scheme, which nothing ever signs into, so every
/// cookie-authenticated /api call (vote, comment, like) 401'd even for signed-in
/// users. Accepting both schemes lets a single endpoint serve web and mobile.
/// Unauthenticated requests still get a clean JSON 401/403 (not a login redirect)
/// because the application cookie is configured to return status codes for /api
/// paths — see AuthExtensions.ConfigureApplicationCookie.
///
/// CSRF: cookie-authenticated mutating requests must still carry an antiforgery
/// token (see <see cref="ConditionalAntiforgeryAttribute"/>); bearer requests do
/// not, since they are not subject to CSRF.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ApiAuthorizeAttribute : AuthorizeAttribute
{
    // Not const: IdentityConstants.ApplicationScheme is static readonly, not a
    // compile-time constant. Resolved once at type load.
    public static readonly string ApiSchemes =
        $"{IdentityConstants.ApplicationScheme},{JwtBearerDefaults.AuthenticationScheme}";

    public ApiAuthorizeAttribute()
    {
        AuthenticationSchemes = ApiSchemes;
    }

    public ApiAuthorizeAttribute(string policy)
        : base(policy)
    {
        AuthenticationSchemes = ApiSchemes;
    }
}
