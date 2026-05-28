using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace FixIt.Extensions;

/// <summary>
/// [Authorize] variant that pins the authentication scheme to JWT bearer.
/// Use on API controllers under /api/* so unauthenticated requests get a clean
/// 401 (and authenticated-but-forbidden get 403) instead of the cookie scheme's
/// 302 redirect to /Identity/Account/Login — which is meaningless to mobile
/// clients.
///
/// Web pages and the Razor-Pages admin area keep using plain [Authorize]; that
/// flows through the global default scheme (Cookie) and preserves redirect-to-
/// login behavior in the browser.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ApiAuthorizeAttribute : AuthorizeAttribute
{
    public ApiAuthorizeAttribute()
    {
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme;
    }

    public ApiAuthorizeAttribute(string policy)
        : base(policy)
    {
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme;
    }
}
