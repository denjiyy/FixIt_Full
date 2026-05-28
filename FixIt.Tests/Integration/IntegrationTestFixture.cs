using System.Net.Http.Headers;
using System.Net.Http.Json;
using FixIt.Extensions;
using FixIt.Models.Users;
using FixIt.Services.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.MongoDb;
using Xunit;

namespace FixIt.Tests.Integration;

/// <summary>
/// Shared per-test-class fixture that spins up a Mongo testcontainer and a
/// WebApplicationFactory&lt;Program&gt; pointed at it. Requires a working Docker
/// daemon on the host (Docker Desktop locally, default on GitHub-hosted runners).
/// Each fixture gets a fresh database name so tests don't cross-contaminate.
/// </summary>
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private MongoDbContainer? _mongo;
    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("Fixture not initialized (Docker likely unavailable).");

    public string DatabaseName { get; } = $"fixit_itest_{Guid.NewGuid():N}";

    /// <summary>
    /// False when the local Docker daemon is unavailable (Docker Desktop not
    /// running, no socket, etc.). Tests should call Skip.IfNot(IsAvailable, ...).
    /// CI on ubuntu-latest always has Docker, so this is purely a local-dev
    /// ergonomic so `dotnet test` doesn't fail when Docker is off.
    /// </summary>
    public bool IsAvailable { get; private set; }

    public string? UnavailabilityReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _mongo = new MongoDbBuilder()
                .WithImage("mongo:7.0")
                .Build();

            await _mongo.StartAsync();
        }
        catch (Exception ex)
        {
            UnavailabilityReason = $"Mongo testcontainer could not start: {ex.GetType().Name}: {ex.Message}";
            _mongo = null;
            return;
        }

        var connectionString = _mongo.GetConnectionString();

        // Several Program.cs startup paths read configuration eagerly during
        // service registration (auth, Mongo bootstrap), before any
        // ConfigureAppConfiguration callbacks have a chance to apply. Setting
        // process env vars is the only override mechanism that lands early
        // enough for that code path. In-memory config is layered on as a
        // belt-and-braces measure for anything that reads later via IConfiguration.
        Environment.SetEnvironmentVariable("MONGODB_URI", connectionString);
        Environment.SetEnvironmentVariable("MONGODB_DATABASE", DatabaseName);
        Environment.SetEnvironmentVariable(
            "JWT_SECRET_KEY",
            "integration-test-jwt-secret-must-be-at-least-32-bytes-long-and-random-enough");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["MongoDB:ConnectionString"] = connectionString,
                        ["MongoDB:DatabaseName"] = DatabaseName,
                        ["Jwt:SecretKey"] = "integration-test-jwt-secret-must-be-at-least-32-bytes-long-and-random-enough",
                        ["Jwt:Issuer"] = "FixIt.Tests",
                        ["Jwt:Audience"] = "FixIt.Tests",
                        ["Security:RateLimiting:Enabled"] = "false",
                        ["Database:EnableDevelopmentAdminSeed"] = "false",
                        ["Database:ResetOnStartup"] = "false",
                    });
                });

                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Warning);
                });
            });

        // Touching Services forces the host to build. If Program startup throws
        // (e.g., Mongo unreachable), we mark unavailable rather than crashing
        // the whole class fixture.
        try
        {
            _ = _factory.Services;
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            UnavailabilityReason = $"WebApplicationFactory failed to build: {ex.GetType().Name}: {ex.Message}";
            _factory.Dispose();
            _factory = null;
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }

    /// <summary>
    /// Provisions an admin user inside the test DB using the same code path as
    /// production bootstrap. Returns (email, password) so the caller can log in.
    /// </summary>
    public async Task<(string Email, string Password)> ProvisionAdminAsync(
        string email = "test-admin@fixit.test",
        string password = "TestAdminPass1")
    {
        using var scope = Factory.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IntegrationTestFixture>>();

        var result = await AdminBootstrapExtensions.EnsureAdminAsync(
            scope.ServiceProvider,
            email: email,
            userName: email,
            displayName: "Test Admin",
            password: password,
            force: true,
            logger: logger);

        if (!result.Ok)
        {
            throw new InvalidOperationException(
                $"Admin provisioning failed in test fixture: {result.Message} ({string.Join("; ", result.Errors)})");
        }

        return (email, password);
    }

    /// <summary>
    /// Provisions a regular (non-admin) user. Returns (email, password).
    /// </summary>
    public async Task<(string Email, string Password)> ProvisionRegularUserAsync(
        string email = "test-user@fixit.test",
        string password = "TestUserPass1")
    {
        // Bypass /api/auth/register to keep this fixture independent of
        // rate-limiting, anti-forgery, and request-pipeline middleware that
        // could obscure test failures. Goes through the same UserManager the
        // controller uses so the resulting user is realistic.
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Test User",
            HasPasswordAuth = true,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to provision regular user: "
                + string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        // No role assignment — for this fixture's purposes "regular user" means
        // explicitly NOT in the Admin role. Whether they're in RoleNames.User or
        // no role at all is irrelevant to the 403 assertion downstream, and
        // skipping it avoids depending on the User role existing in the store.
        return (email, password);
    }

    /// <summary>
    /// Logs the given credentials in via /api/auth/login and returns the access
    /// token, plus an HttpClient pre-configured with the bearer header.
    /// </summary>
    public async Task<(string AccessToken, HttpClient AuthedClient)> LoginAndGetClientAsync(
        string email,
        string password)
    {
        var client = Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();

        var envelope = await login.Content.ReadFromJsonAsync<ApiEnvelope<TokenPayload>>()
            ?? throw new InvalidOperationException("Login response was not a parseable envelope.");

        var token = envelope.Data?.AccessToken
            ?? throw new InvalidOperationException("Login envelope had no access token.");

        // AllowAutoRedirect=false so we see the real status from the API endpoint.
        // Cookie auth (the default scheme for [Authorize] on these controllers)
        // would otherwise 302 → /Identity/Account/Login → 200 OK and silently
        // mask both authentication and authorization failures.
        var authed = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (token, authed);
    }

    public sealed record ApiEnvelope<T>(T? Data, string? Message, bool Success);
    public sealed record TokenPayload(string? AccessToken, string? RefreshToken);
}
