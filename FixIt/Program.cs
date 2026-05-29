using System.Globalization;
using System.Security.Cryptography;
using FixIt.Data.Infrastructure;
using FixIt.Data.Infrastructure.Migrations;
using FixIt.Extensions;
using FixIt.Models.Infrastructure;
using FixIt.Models.Users;
using FixIt.Services.Constants;
using MongoDB.Driver;
using FixIt.Services.Contracts;
using FixIt.Services.Gamification;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
var isProduction = builder.Environment.IsProduction();

// Normalize allowed hosts so both comma- and semicolon-delimited env values are supported.
var allowedHostsRaw = builder.Configuration["AllowedHosts"];
if (!string.IsNullOrWhiteSpace(allowedHostsRaw))
{
    var normalizedAllowedHosts = allowedHostsRaw
        .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (normalizedAllowedHosts.Length > 0)
    {
        builder.Services.Configure<HostFilteringOptions>(options =>
        {
            options.AllowedHosts.Clear();
            foreach (var host in normalizedAllowedHosts)
            {
                options.AllowedHosts.Add(host);
            }
        });
    }
}

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddRazorPages(options =>
    {
        options.Conventions.AuthorizeAreaFolder("Admin", "/", PolicyNames.AdminArea);
        options.Conventions.AllowAnonymousToAreaPage("Admin", "/Login");
    })
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isProduction
        ? Microsoft.AspNetCore.Http.CookieSecurePolicy.Always
        : Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
});

var securityConfig = builder.Configuration.GetSection("Security");
var isRateLimitingEnabled = builder.Configuration.GetSection("Security:RateLimiting").GetValue("Enabled", true);
builder.Services.AddFixItRateLimiting(builder.Configuration);

// Mongo client/database + repository registrations, returning the resolved
// connection string for Identity to reuse.
var (_, mongoConnectionString, _) = builder.Services.AddMongoDb(builder.Configuration);
builder.Services.AddIdentityWithMongo(mongoConnectionString);

// ========== STARTUP DIAGNOSTICS ==========
// Kept inline per the operational-clarity rule so deployment configuration is
// visible to readers without indirection.
if (isProduction)
{
    Console.WriteLine("[STARTUP] === Production Configuration Diagnostics ===");
    var requiredVars = new[] { "MONGODB_URI", "MONGODB_DATABASE", "JWT_SECRET_KEY", "GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET" };
    foreach (var varName in requiredVars)
    {
        // Presence/absence only — never log any portion of a configuration value,
        // since several of these (JWT_SECRET_KEY, GOOGLE_CLIENT_SECRET) are secrets
        // and even a prefix is sensitive.
        var isSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(varName));
        Console.WriteLine($"[STARTUP] {varName}: {(isSet ? "✓ set" : "✗ NOT SET")}");
    }
    Console.WriteLine("[STARTUP] === End Diagnostics ===");
}

var authResult = builder.Services.AddFixItAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddFixItBusinessServices(builder.Configuration);
builder.Services.AddFixItCloudinary(builder.Configuration, builder.Environment);
builder.Services.AddFixItCors(builder.Configuration, builder.Environment);
builder.Services.AddFixItCachingAndCompression();
builder.Services.AddFixItDataProtection(builder.Configuration);

// Logging configuration
builder.Services.AddLogging(config =>
{
    config.ClearProviders();
    if (builder.Environment.IsDevelopment())
    {
        config.AddConsole();
        config.AddDebug();
        config.SetMinimumLevel(LogLevel.Information);
    }
    else
    {
        config.AddConsole();
        config.SetMinimumLevel(LogLevel.Warning);
        config.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        config.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        config.AddFilter("MongoDB.Driver", LogLevel.Warning);
    }
});

var supportedCultures = new[]
{
    new CultureInfo("en-US"),
    new CultureInfo("bg-BG")
};

var localizationOptions = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
};
localizationOptions.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.QueryStringRequestCultureProvider());
localizationOptions.RequestCultureProviders.Insert(1, new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());

var app = builder.Build();

// ========== CLI: ADMIN BOOTSTRAP ==========
// Short-circuit before any web/server setup when invoked with --bootstrap-admin.
// This is the supported path for creating the initial admin in production (and
// any env without an existing admin), since the development seeder below is
// gated to IsDevelopment().
if (AdminBootstrapExtensions.IsBootstrapRequested(args))
{
    var bootstrapExit = await app.RunAdminBootstrapAsync(args);
    await app.DisposeAsync();
    Environment.Exit(bootstrapExit);
}

if (!authResult.GoogleConfigured)
{
    if (isProduction)
    {
        app.Logger.LogCritical("Google OAuth credentials are missing or invalid in production.");
    }
    else
    {
        app.Logger.LogWarning("Google OAuth is disabled because Authentication:Google credentials are missing or placeholder values.");
    }
}

if (isProduction && string.IsNullOrWhiteSpace(builder.Configuration["DataProtection:KeyRingPath"] ?? builder.Configuration["DATA_PROTECTION_KEY_RING_PATH"]))
{
    app.Logger.LogWarning("DataProtection:KeyRingPath is not configured. Auth cookies may be invalidated after container restarts.");
}

if (isProduction && string.IsNullOrWhiteSpace(builder.Configuration["DataProtection:CertificatePath"]))
{
    app.Logger.LogWarning("DataProtection:CertificatePath is not configured. Data protection keys are persisted without an explicit at-rest encryptor.");
}

// ========== DATABASE SEEDING ==========
// Inline per operational-clarity rule: seed/reset behaviour must remain visible
// to readers, since flipping Database:ResetOnStartup is irreversible.
try
{
    using (var scope = app.Services.CreateScope())
    {
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var shouldResetDb = string.Equals(builder.Configuration["Database:ResetOnStartup"], "true", StringComparison.OrdinalIgnoreCase)
            && app.Environment.IsDevelopment();

        if (shouldResetDb)
        {
            logger.LogWarning("⚠️ DATABASE RESET REQUESTED - Clearing all collections (development only)");
            var collectionNames = (await mongoContext.Database.ListCollectionNamesAsync()).ToList();
            foreach (var collectionName in collectionNames)
            {
                logger.LogInformation("Dropping collection: {Collection}", collectionName);
                await mongoContext.Database.DropCollectionAsync(collectionName);
            }
            logger.LogInformation("Database cleared successfully. Dropping {CollectionCount} collections.", collectionNames.Count);

            try
            {
                await mongoContext.Database.DropCollectionAsync(MongoCollectionNames.Users);
                logger.LogInformation("Explicitly dropped {Collection} collection.", MongoCollectionNames.Users);
            }
            catch
            {
                logger.LogInformation("{Collection} collection was already dropped or doesn't exist.", MongoCollectionNames.Users);
            }
        }

        await MongoCollectionNamingMigration.RunAsync(mongoContext.Database, logger);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        try
        {
            int existingUsers;
            try
            {
                existingUsers = userManager.Users.Count();
            }
            catch
            {
                logger.LogInformation("Unable to query users (expected after reset). Proceeding with seeding.");
                existingUsers = 0;
            }

            if (existingUsers == 0 || shouldResetDb)
            {
                // Demo content (sample issues with a fake "Civic Reporter" author)
                // is only seeded outside production. Indexes and reference data
                // (cities, tags) run everywhere — the report-issue flow needs them.
                var seedDemoData = !isProduction;
                logger.LogInformation(
                    "Database is empty or reset requested. Running seeders (seedDemoData={SeedDemoData})...",
                    seedDemoData);
                await SeederRunner.RunAllConfiguratorsAsync(mongoContext.Database, scope.ServiceProvider, seedDemoData);
                logger.LogInformation("Seeder configurations completed.");

                var shouldSeedDevelopmentAdmin = app.Environment.IsDevelopment()
                    && builder.Configuration.GetValue<bool>("Database:EnableDevelopmentAdminSeed");

                if (shouldSeedDevelopmentAdmin)
                {
                    var adminEmail = builder.Configuration["Database:DevelopmentAdmin:Email"] ?? "admin@fixit.local";
                    var adminUserName = builder.Configuration["Database:DevelopmentAdmin:UserName"] ?? "admin";
                    var adminDisplayName = builder.Configuration["Database:DevelopmentAdmin:DisplayName"] ?? "Admin User";
                    var adminPassword = builder.Configuration["Database:DevelopmentAdmin:Password"];

                    if (string.IsNullOrWhiteSpace(adminPassword))
                    {
                        logger.LogWarning("Development admin seed requested, but Database:DevelopmentAdmin:Password is missing. Skipping admin seed.");
                    }
                    else
                    {
                        logger.LogInformation("Attempting to create development admin user with email: {AdminEmail}", adminEmail);

                        var seedResult = await AdminBootstrapExtensions.EnsureAdminAsync(
                            scope.ServiceProvider,
                            email: adminEmail,
                            userName: adminUserName,
                            displayName: adminDisplayName,
                            password: adminPassword,
                            force: true,
                            logger: logger);

                        if (seedResult.Ok)
                        {
                            logger.LogInformation("Development admin user created successfully.");
                            logger.LogWarning("Development admin seed is enabled. Disable Database:EnableDevelopmentAdminSeed for non-bootstrap runs.");
                        }
                        else
                        {
                            logger.LogError(
                                "Failed to create development admin user: {Message} Errors: {Errors}",
                                seedResult.Message,
                                string.Join("; ", seedResult.Errors));
                        }
                    }
                }
                else
                {
                    logger.LogInformation("Development admin seed disabled. Skipping admin user bootstrap.");
                }

                var reputationService = scope.ServiceProvider.GetRequiredService<IReputationService>();
                logger.LogInformation("Regenerating leaderboards (all time, monthly, weekly)...");
                await reputationService.RegenerateAllTimeLeaderboardAsync();
                await reputationService.RegenerateMonthlyLeaderboardAsync();
                await reputationService.RegenerateWeeklyLeaderboardAsync();
                logger.LogInformation("Database seeding and leaderboard generation completed.");
            }
            else
            {
                logger.LogInformation("Database already initialized with {UserCount} users. Skipping seeding.", existingUsers);

                var reputationService = scope.ServiceProvider.GetRequiredService<IReputationService>();
                logger.LogInformation("Regenerating leaderboards (all time, monthly, weekly)...");
                await reputationService.RegenerateAllTimeLeaderboardAsync();
                await reputationService.RegenerateMonthlyLeaderboardAsync();
                await reputationService.RegenerateWeeklyLeaderboardAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database seeding and admin user creation");
        }

        // ========== ADMIN-EXISTENCE GUARD ==========
        // In production, surface a CRITICAL warning if no admin users exist.
        // We don't crash — that creates a chicken-and-egg with the bootstrap CLI.
        // But silent operation in this state is exactly how the prod admin gap
        // shipped previously. Failing loud beats failing silent.
        if (isProduction)
        {
            try
            {
                var adminCheckUserManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var admins = await adminCheckUserManager.GetUsersInRoleAsync(RoleNames.Admin);
                if (admins.Count == 0)
                {
                    logger.LogCritical(
                        "No users with the Admin role exist in production. The admin surface (/admin/*) is unreachable until an admin is provisioned. Run: dotnet run -- --bootstrap-admin --email <e> --password <p>");
                }
                else
                {
                    logger.LogInformation("Admin role population check: {AdminCount} admin user(s) present.", admins.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Admin-existence guard failed to query the Admin role.");
            }
        }

        // ========== ROLE-DRIFT AUDIT ==========
        // The codebase carries two role representations (ApplicationUser.Role
        // enum + Identity role claims). UserRoleSync.SetUserRoleAsync keeps
        // them in sync going forward; this audit catches any users where they
        // already disagree (legacy data, partial writes, manual DB edits).
        // Warning only — not an error. Fix via the admin Users page.
        try
        {
            await UserRoleSync.AuditRoleDriftAsync(scope.ServiceProvider, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Role-drift audit failed; skipping.");
        }
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Failed to initialize database. Make sure MongoDB is reachable.");
}

// ========== MIDDLEWARE PIPELINE ==========
// Inline per operational-clarity rule: middleware ordering is the single most
// brittle piece of ASP.NET configuration and must read top-to-bottom.
app.UseMiddleware<FixIt.Middleware.GlobalExceptionHandlingMiddleware>();
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FixIt API v1");
        c.RoutePrefix = "swagger";
        c.DefaultModelsExpandDepth(-1);
    });
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), payment=()";
    var csp = securityConfig.GetValue<string>("ContentSecurityPolicy");
    if (!string.IsNullOrEmpty(csp))
    {
        context.Response.Headers["Content-Security-Policy"] = csp;
    }
    await next();
});

app.UseResponseCompression();

// Cache-control + selective ETag for JSON API responses
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var method = context.Request.Method;

    var isGetOrHead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
    var isApiRequest = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
    var isHealthRequest =
        path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase);
    var isStaticAsset =
        path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase);

    if (!isGetOrHead)
    {
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    }
    else if (isStaticAsset)
    {
        context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
    }
    else if (isHealthRequest)
    {
        context.Response.Headers["Cache-Control"] = "public, max-age=30";
    }
    else if (isApiRequest)
    {
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    }
    else if (context.Request.Headers.ContainsKey("Cookie"))
    {
        context.Response.Headers["Cache-Control"] = "private, no-cache, max-age=0, must-revalidate";
    }

    var isMediaEndpoint =
        path.Contains("/api/media", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/api/uploads", StringComparison.OrdinalIgnoreCase);
    var canBufferForEtag = isGetOrHead && isApiRequest && !isHealthRequest && !isMediaEndpoint;

    if (!canBufferForEtag)
    {
        await next();
        return;
    }

    var originalBody = context.Response.Body;
    await using var memoryStream = new MemoryStream();
    context.Response.Body = memoryStream;
    await next();
    context.Response.Body = originalBody;

    var responseBytes = memoryStream.ToArray();
    var contentType = context.Response.ContentType ?? string.Empty;
    var isJsonResponse =
        contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("+json", StringComparison.OrdinalIgnoreCase);

    if (isJsonResponse && context.Response.StatusCode == StatusCodes.Status200OK && responseBytes.Length > 0 && responseBytes.Length < 1_000_000)
    {
        var hashBytes = SHA256.HashData(responseBytes);
        var etag = $"\"{Convert.ToHexString(hashBytes)}\"";
        context.Response.Headers["ETag"] = etag;

        if (context.Request.Headers.TryGetValue("If-None-Match", out var clientEtag) &&
            clientEtag.ToString().Split(',').Select(v => v.Trim()).Any(v => v == "*" || v.Equals(etag, StringComparison.Ordinal)))
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            context.Response.ContentLength = 0;
            return;
        }
    }

    context.Response.ContentLength = responseBytes.Length;
    await originalBody.WriteAsync(responseBytes);
});

app.UseStaticFiles();
app.UseRequestLocalization(localizationOptions);
app.UseRouting();
app.UseCors("ProductionCors");
app.UseOutputCache();

if (isRateLimitingEnabled)
{
    app.UseRateLimiter();
}

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/404");

app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode >= 500 && context.Response.StatusCode < 600 && !context.Response.HasStarted)
    {
        context.Response.Redirect($"/error?code={context.Response.StatusCode}", permanent: false);
    }
});

using (var scope = app.Services.CreateScope())
{
    var migrationRunner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    await migrationRunner.RunPendingMigrationsAsync();
}

var controllers = app.MapControllers();
if (isRateLimitingEnabled)
{
    controllers.RequireRateLimiting(RateLimitPolicyNames.Api);
}

var razorPages = app.MapRazorPages();
if (isRateLimitingEnabled)
{
    razorPages.RequireRateLimiting(RateLimitPolicyNames.Api);
}

app.MapAreaControllerRoute(
    name: "Identity",
    areaName: "Identity",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapFallback(context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return context.Response.WriteAsync(string.Empty);
});

// Railway sets PORT; fall back to 8080 otherwise.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var urls = $"http://0.0.0.0:{port}";
app.Urls.Add(urls);
app.Logger.LogInformation("Application configured to listen on {URLs}", urls);

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
