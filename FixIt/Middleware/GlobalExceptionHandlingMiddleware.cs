using FixIt.Data.Repository;
using FixIt.ViewModels;

namespace FixIt.Middleware;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);

        if (context.Response.HasStarted)
        {
            return;
        }

        var (statusCode, message) = exception switch
        {
            InvalidEntityIdException => (StatusCodes.Status400BadRequest, "Invalid identifier format."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request data."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        var isApiRequest = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
        if (isApiRequest)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.CreateError(message));
            return;
        }

        // For web page requests, redirect to appropriate error page
        if (statusCode == StatusCodes.Status404NotFound)
        {
            context.Response.Redirect("/404", permanent: false);
        }
        else if (statusCode == StatusCodes.Status403Forbidden)
        {
            context.Response.Redirect("/error?code=403", permanent: false);
        }
        else
        {
            // All other errors (500, etc.) redirect to error page
            context.Response.Redirect("/error", permanent: false);
        }
    }
}
