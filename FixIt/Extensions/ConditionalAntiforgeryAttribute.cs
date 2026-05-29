using FixIt.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FixIt.Extensions;

/// <summary>
/// Antiforgery (CSRF) validation that applies only to cookie-authenticated
/// browser requests. Bearer-token requests (mobile / external API clients) are
/// not subject to CSRF — they carry no ambient credentials — so requiring an
/// antiforgery token for them would needlessly break those clients.
///
/// Use this on mutating /api endpoints that are reachable via BOTH cookie and
/// bearer auth (i.e. anything marked <see cref="ApiAuthorizeAttribute"/>) in
/// place of a plain <c>[ValidateAntiForgeryToken]</c>.
///
/// Behaviour:
///   - Safe methods (GET/HEAD/OPTIONS/TRACE): never validated.
///   - Requests with an <c>Authorization: Bearer ...</c> header: skipped.
///   - All other (cookie) requests: antiforgery token is required; a missing or
///     invalid token yields a JSON 400 instead of the framework's default 400
///     HTML/empty body, keeping the API contract consistent.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ConditionalAntiforgeryAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        => new ConditionalAntiforgeryFilter(serviceProvider.GetRequiredService<IAntiforgery>());

    private sealed class ConditionalAntiforgeryFilter : IAsyncAuthorizationFilter
    {
        private readonly IAntiforgery _antiforgery;

        public ConditionalAntiforgeryFilter(IAntiforgery antiforgery) => _antiforgery = antiforgery;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var request = context.HttpContext.Request;

            if (HttpMethods.IsGet(request.Method) ||
                HttpMethods.IsHead(request.Method) ||
                HttpMethods.IsOptions(request.Method) ||
                HttpMethods.IsTrace(request.Method))
            {
                return;
            }

            // Bearer-token requests are immune to CSRF; do not require a token.
            string authorization = request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                await _antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new ObjectResult(
                    ApiResponse<object>.CreateError("Invalid or missing antiforgery token."))
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }
        }
    }
}
