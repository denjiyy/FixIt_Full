using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace FixIt.Extensions;

/// <summary>
/// [Authorize] variant for /api/* endpoints that accepts BOTH authentication
/// schemes:
///   - JWT bearer  → mobile / external API clients
///   - Cookie      → browser (Razor pages) calling the same JSON endpoints
///
/// Previously this pinned bearer only, which 401'd browser cookie calls to /api
/// even though the user was signed in. Accepting both schemes lets a single
/// endpoint serve web and mobile. Unauthenticated requests still get a clean
/// JSON 401/403 (not a login redirect) because the cookie handler is configured
/// to return status codes for /api paths — see AuthExtensions.
///
/// CSRF: cookie-authenticated mutating requests must still carry an antiforgery
/// token (see <see cref="ConditionalAntiforgeryAttribute"/>); bearer requests do
/// not, since they are not subject to CSRF.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ApiAuthorizeAttribute : AuthorizeAttribute
{
    public const string ApiSchemes =
        $"{CookieAuthenticationDefaults.AuthenticationScheme},{JwtBearerDefaults.AuthenticationScheme}";

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
