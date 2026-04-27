using System.Text.Json;
using FixIt.Data.Repository;
using FixIt.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace FixIt.Tests.Middleware;

public class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenInvalidEntityIdException_ReturnsBadRequestPayload()
    {
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new InvalidEntityIdException("bad-id"),
            Mock.Of<ILogger<GlobalExceptionHandlingMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/issues/bad-id";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Invalid identifier format.", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_ReturnsInternalServerErrorPayload()
    {
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new Exception("unexpected"),
            Mock.Of<ILogger<GlobalExceptionHandlingMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/health";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("An unexpected error occurred.", document.RootElement.GetProperty("message").GetString());
    }
}
