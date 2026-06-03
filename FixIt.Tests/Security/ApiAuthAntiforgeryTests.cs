using FixIt.Extensions;
using FixIt.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FixIt.Tests.Security;

/// <summary>
/// Verifies the /api authentication + CSRF pattern: ApiAuthorize accepts both
/// cookie and bearer schemes, and ConditionalAntiforgery enforces antiforgery
/// only for cookie-authenticated browser requests (never for bearer/mobile).
/// </summary>
public class ApiAuthAntiforgeryTests
{
    [Fact]
    public void ApiAuthorize_AcceptsBothCookieAndBearerSchemes()
    {
        var attribute = new ApiAuthorizeAttribute();

        Assert.NotNull(attribute.AuthenticationSchemes);
        var schemes = attribute.AuthenticationSchemes!.Split(',', StringSplitOptions.TrimEntries);
        // The cookie scheme must be the Identity application cookie — the one
        // SignInManager actually writes. The generic "Cookies" scheme is never
        // signed into, so listing it 401'd real cookie users (see
        // CookieApiAuthIntegrationTests).
        Assert.Contains(IdentityConstants.ApplicationScheme, schemes);
        Assert.Contains(JwtBearerDefaults.AuthenticationScheme, schemes);
    }

    [Fact]
    public async Task ConditionalAntiforgery_SkipsValidation_ForBearerRequests()
    {
        var antiforgery = new Mock<IAntiforgery>(MockBehavior.Strict); // any call => test fails
        var context = CreateContext("POST", bearer: true, antiforgery.Object);

        await InvokeFilterAsync(context, antiforgery.Object);

        Assert.Null(context.Result); // request allowed through, no antiforgery enforced
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task ConditionalAntiforgery_SkipsValidation_ForSafeMethods()
    {
        var antiforgery = new Mock<IAntiforgery>(MockBehavior.Strict);
        var context = CreateContext("GET", bearer: false, antiforgery.Object);

        await InvokeFilterAsync(context, antiforgery.Object);

        Assert.Null(context.Result);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task ConditionalAntiforgery_Enforces_ForCookieMutatingRequests()
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("missing token"));

        var context = CreateContext("POST", bearer: false, antiforgery.Object);

        await InvokeFilterAsync(context, antiforgery.Object);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var payload = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.False(payload.Success);
    }

    [Fact]
    public async Task ConditionalAntiforgery_Allows_CookieRequestWithValidToken()
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .Returns(Task.CompletedTask);

        var context = CreateContext("POST", bearer: false, antiforgery.Object);

        await InvokeFilterAsync(context, antiforgery.Object);

        Assert.Null(context.Result);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Once);
    }

    private static AuthorizationFilterContext CreateContext(string method, bool bearer, IAntiforgery antiforgery)
    {
        var services = new ServiceCollection();
        services.AddSingleton(antiforgery);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        httpContext.Request.Method = method;
        if (bearer)
        {
            httpContext.Request.Headers.Authorization = "Bearer test-token";
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static async Task InvokeFilterAsync(AuthorizationFilterContext context, IAntiforgery antiforgery)
    {
        var factory = new ConditionalAntiforgeryAttribute();
        var services = new ServiceCollection();
        services.AddSingleton(antiforgery);
        var filter = (IAsyncAuthorizationFilter)factory.CreateInstance(services.BuildServiceProvider());
        await filter.OnAuthorizationAsync(context);
    }
}
