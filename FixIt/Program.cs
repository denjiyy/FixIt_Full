using MongoDB.Driver;
using CloudinaryDotNet;
using AspNetCore.Identity.Mongo;
using AspNetCore.Identity.Mongo.Model;
using FixIt.Models.Users;
using FixIt.Models.Infrastructure;
using FixIt.Models.Enums;
using FixIt.Models.Gamification;
using FixIt.Data.Infrastructure;
using FixIt.Data.Infrastructure.Migrations;
using FixIt.Data.Repository;
using FixIt.Data.Repository.Contracts;
using FixIt.Services;
using FixIt.Services.Storage;
using FixIt.Services.Contracts;
using FixIt.Services.Authentication;
using FixIt.Services.Gamification;
using FixIt.Services.AI;
using FixIt.Services.Analytics;
using FixIt.Services.Safety;
using FixIt.Services.Background;
using FixIt.Services.Email;
using FixIt.Services.Constants;
using FixIt.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
var builder = WebApplication.CreateBuilder(args);
var isProduction = builder.Environment.IsProduction();

// Resolve placeholder-style environment/config expressions like:
// - ${MONGODB_URI}
// - ${MONGODB_URI:someDefault}
static string? ResolveRailwayStylePlaceholder(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return value;

    var trimmed = value.Trim();

    // Fast path: only attempt placeholder resolution when string contains "${"
    if (!trimmed.Contains("${", StringComparison.Ordinal))
        return value;

    // Support a single placeholder pattern for now (sufficient for connection strings).
    // Examples matched:
    //   ${MONGODB_URI}
    //   ${MONGODB_URI:defaultValue}
    var start = trimmed.IndexOf("${", StringComparison.Ordinal);
    var end = trimmed.IndexOf('}', start + 2);
    if (start < 0 || end < 0)
        return value;

    var placeholderBody = trimmed.Substring(start + 2, end - (start + 2)); // "MONGODB_URI" or "MONGODB_URI:default"
    var parts = placeholderBody.Split(':', 2, StringSplitOptions.None);
    var envVarName = parts[0].Trim();

    if (string.IsNullOrWhiteSpace(envVarName))
        return value;

    var envValue = Environment.GetEnvironmentVariable(envVarName);
    if (!string.IsNullOrWhiteSpace(envValue))
        return envValue;

    // default value (optional)
    if (parts.Length == 2)
    {
        var defaultValue = parts[1];
        return defaultValue ?? value;
    }

    return value;
}

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

// Localization configuration
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Add services to the container.
builder.Services.AddRazorPages(options =>
    {
        options.Conventions.AuthorizeAreaFolder("Admin", "/", PolicyNames.AdminOnly);
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

// Configure session for tracking anonymous user views
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isProduction ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});

// Configure Rate Limiting - Store security config for later use
var securityConfig = builder.Configuration.GetSection("Security");
var rateLimitingConfig = builder.Configuration.GetSection("Security:RateLimiting");
var isRateLimitingEnabled = rateLimitingConfig.GetValue("Enabled", true);
var configuredHttpsPort = securityConfig.GetValue<int?>("HttpsPort");
var httpsPort = configuredHttpsPort.HasValue && configuredHttpsPort.Value > 0 ? configuredHttpsPort.Value : 443;
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = httpsPort;
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
});

// Configure rate limiting (DDoS protection) with per-client partitioning
if (isRateLimitingEnabled)
{
    builder.Services.AddRateLimiter(rateLimiterOptions =>
    {
        rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        
        // Global rate limit key policy: partition by user ID (authenticated) or IP address (anonymous)
        rateLimiterOptions.AddPolicy("PerClientKey", context =>
        {
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var clientKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
        });

        // Strict policy for authentication endpoints: 5 attempts per 15 minutes per user/IP
        rateLimiterOptions.AddPolicy(RateLimitPolicyNames.AuthStrict, context =>
        {
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var clientKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
        });

        // API policy: 60 per minute per user/IP
        rateLimiterOptions.AddPolicy(RateLimitPolicyNames.Api, context =>
        {
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var clientKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
        });

        // Reporting/Analytics: 30 per minute per user/IP
        rateLimiterOptions.AddPolicy(RateLimitPolicyNames.Reporting, context =>
        {
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var clientKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
        });

        // Upload policy: 10 per minute per user/IP
        rateLimiterOptions.AddPolicy(RateLimitPolicyNames.Upload, context =>
        {
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var clientKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
        });
    });
}

/*
Configure MongoDB

Priority:
1) Environment variable (MONGODB_URI) (and MONGODB_DATABASE)
2) appsettings.json fallback (MongoDB:ConnectionString, MongoDB:DatabaseName)

Additionally, Railway sometimes leaves placeholder expressions as-is (e.g. "${MONGODB_URI}")
which then reach the Mongo driver and fail parsing with a confusing error.
We resolve simple placeholder expressions and validate that the final string is not still a placeholder.
*/

/*
Priority: Environment variable (MONGODB_URI) → appsettings.json fallback (MongoDB:ConnectionString)

Railway may surface placeholder expressions (e.g. "${MONGODB_URI:}") as literal strings via
appsettings/deployment variables. Resolve these expressions before handing them to the Mongo driver.
*/

// Priority: Environment variable (MONGODB_URI) → appsettings.json fallback
var mongoConnectionStringRaw =
    Environment.GetEnvironmentVariable("MONGODB_URI")
    ?? builder.Configuration["MongoDB:ConnectionString"];

mongoConnectionStringRaw = ResolveRailwayStylePlaceholder(mongoConnectionStringRaw);

if (string.IsNullOrWhiteSpace(mongoConnectionStringRaw))
{
    throw new InvalidOperationException(
        "MongoDB connection string not found. Set MONGODB_URI environment variable (or MongoDB:ConnectionString in appsettings.json).");
}

if (mongoConnectionStringRaw.Contains("${", StringComparison.Ordinal) || mongoConnectionStringRaw.Contains("}", StringComparison.Ordinal))
{
    // Keep this deliberately strict and explicit for deployment diagnostics.
    throw new InvalidOperationException(
        "MongoDB connection string still contains a placeholder expression. " +
        "Railway likely did not substitute MONGODB_URI. " +
        $"Value received: '{mongoConnectionStringRaw}'.");
}

var mongoConnectionString = mongoConnectionStringRaw;

var mongoDatabaseNameRaw =
    Environment.GetEnvironmentVariable("MONGODB_DATABASE")
    ?? builder.Configuration["MongoDB:DatabaseName"];

mongoDatabaseNameRaw = ResolveRailwayStylePlaceholder(mongoDatabaseNameRaw);

if (string.IsNullOrWhiteSpace(mongoDatabaseNameRaw))
{
    throw new InvalidOperationException(
        "MongoDB database name not found. Set MONGODB_DATABASE environment variable (or MongoDB:DatabaseName in appsettings.json).");
}

if (mongoDatabaseNameRaw.Contains("${", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "MongoDB database name still contains a placeholder expression. " +
        "Railway likely did not substitute MONGODB_DATABASE. " +
        $"Value received: '{mongoDatabaseNameRaw}'.");
}

var mongoDatabaseName = mongoDatabaseNameRaw;

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection(MongoDbSettings.SectionName));

// Register MongoClient as singleton with connection pooling and optimizations
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString(mongoConnectionString);
    // Optimize connection pooling
    settings.MaxConnectionPoolSize = 100;
    settings.MinConnectionPoolSize = 10;
    settings.MaxConnecting = 50;
    // Optimize read preferences for better performance
    settings.ReadPreference = ReadPreference.PrimaryPreferred;
    settings.ConnectTimeout = TimeSpan.FromSeconds(10);
    settings.SocketTimeout = TimeSpan.FromSeconds(30);
    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(30); // Increased from 10 to 30 for Railway
    
    // Configure SSL/TLS based on connection string type
    if (mongoConnectionString.Contains("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
    {
        // MongoDB Atlas requires TLS
        settings.UseTls = true;
        
        // For Railway/container environments: we need to be lenient with TLS
        // The environment can't validate certificates properly, but we trust MongoDB Atlas
        settings.AllowInsecureTls = true;  // Required for Railway containers
        
        // Disable certificate revocation checking (CRL servers not accessible in containers)
        var sslSettings = new SslSettings 
        { 
            CheckCertificateRevocation = false
        };
        settings.SslSettings = sslSettings;
    }
    else
    {
        // Local/self-managed MongoDB: disable TLS (unless explicitly enabled in connection string)
        if (!mongoConnectionString.Contains("tls=true", StringComparison.OrdinalIgnoreCase) &&
            !mongoConnectionString.Contains("ssl=true", StringComparison.OrdinalIgnoreCase))
        {
            settings.UseTls = false;
        }
    }
    
    return new MongoClient(settings);
});

// Register MongoDbContext
builder.Services.AddSingleton<MongoDbContext>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return new MongoDbContext(client, mongoDatabaseName);
});

// Register IMongoDatabase (needed for Repository<T>)
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

// Register migration runner for schema versioning
builder.Services.AddScoped<MigrationRunner>();

// Configure ASP.NET Core Identity with MongoDB
builder.Services.AddIdentityMongoDbProvider<ApplicationUser, MongoRole>(identity =>
{
    identity.Password.RequireDigit = true;
    identity.Password.RequiredLength = 8;
    identity.Password.RequireNonAlphanumeric = false;
    identity.Password.RequireUppercase = true;
    identity.Password.RequireLowercase = true;
    identity.User.RequireUniqueEmail = true;
    
    // Configure lockout settings
    identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    identity.Lockout.MaxFailedAccessAttempts = 5; // Lock after 5 failed attempts
    identity.Lockout.AllowedForNewUsers = true;
},
mongo =>
{
    mongo.ConnectionString = mongoConnectionString;
});

// Register custom claims principal factory to include user role in claims
// This allows User.IsInRole() to work correctly in Razor pages
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.AdminOnly, policy => policy.RequireRole(RoleNames.Admin));
});

// Configure OAuth Authentication
var authConfig = builder.Configuration.GetSection("Authentication");
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isProduction ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
});

// Add Google OAuth
var googleSection = authConfig.GetSection("Google");
var googleClientId = googleSection["ClientId"];
var googleClientSecret = googleSection["ClientSecret"];
var hasGoogleClientId = StartupConfiguration.IsConfiguredSecret(googleClientId);
var hasGoogleClientSecret = StartupConfiguration.IsConfiguredSecret(googleClientSecret);
if (hasGoogleClientId && hasGoogleClientSecret)
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId!.Trim();
        options.ClientSecret = googleClientSecret!.Trim();
        options.CallbackPath = "/signin-google";
    });
}
else if (isProduction)
{
    throw new InvalidOperationException("Google OAuth credentials are not configured. Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET environment variables.");
}

// Add JWT Bearer authentication for mobile/API clients
// This allows same endpoints to be used by both web (cookies) and mobile (JWT tokens) clients
var jwtConfig = builder.Configuration.GetSection("Jwt");
var jwtSecretKey = jwtConfig["SecretKey"];

if (!StartupConfiguration.IsConfiguredSecret(jwtSecretKey) || jwtSecretKey!.Trim().Length < 32)
{
    throw new InvalidOperationException(
        "JWT secret key is not configured or too weak. Set Jwt:SecretKey via environment variable (Jwt__SecretKey) to a strong random secret with at least 32 characters.");
}

var key = System.Text.Encoding.ASCII.GetBytes(jwtSecretKey.Trim());
var issuer = jwtConfig["Issuer"] ?? "FixIt";
var audience = jwtConfig["Audience"] ?? "FixItClients";

authBuilder.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(0)
    };
});

// Register JWT token service for access token and refresh token generation
builder.Services.AddScoped<FixIt.Services.Authentication.ITokenService, FixIt.Services.Authentication.JwtTokenService>();

// Register repositories with correct collection names
builder.Services.AddScoped<IRepository<FixIt.Models.Locations.City>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Locations.City>(db, MongoCollectionNames.Cities);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Locations.Neighborhood>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Locations.Neighborhood>(db, MongoCollectionNames.Neighborhoods);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Issues.Tag>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Issues.Tag>(db, MongoCollectionNames.Tags);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Issues.Issue>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Issues.Issue>(db, MongoCollectionNames.Issues);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Engagement.Vote>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Engagement.Vote>(db, MongoCollectionNames.Votes);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Issues.ViewEvent>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Issues.ViewEvent>(db, MongoCollectionNames.ViewEvents);
});

// Gamification repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Gamification.UserReputation>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Gamification.UserReputation>(db, MongoCollectionNames.UserReputations);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Gamification.ReputationTransaction>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Gamification.ReputationTransaction>(db, MongoCollectionNames.ReputationTransactions);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Gamification.LeaderboardEntry>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Gamification.LeaderboardEntry>(db, MongoCollectionNames.Leaderboards);
});

// Safety (Hazard) repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Safety.Hazard>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Safety.Hazard>(db, MongoCollectionNames.Hazards);
});

// User repository for dependency injection
builder.Services.AddScoped<IRepository<FixIt.Models.Users.ApplicationUser>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Users.ApplicationUser>(db, MongoCollectionNames.Users);
});

// AI Analysis repositories
builder.Services.AddScoped<IRepository<FixIt.Models.AI.IssueAnalysis>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.AI.IssueAnalysis>(db, MongoCollectionNames.IssueAnalyses);
});

// Admin Suggestions repository
builder.Services.AddScoped<IRepository<FixIt.Models.AI.AdminSuggestion>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.AI.AdminSuggestion>(db, MongoCollectionNames.AdminSuggestions);
});

// Media repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Media.Media>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Media.Media>(db, MongoCollectionNames.Media);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Media.MediaReference>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Media.MediaReference>(db, MongoCollectionNames.MediaReferences);
});

// Engagement repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Engagement.Comment>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Engagement.Comment>(db, MongoCollectionNames.Comments);
});

// Moderation repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Moderation.ContentReport>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Moderation.ContentReport>(db, MongoCollectionNames.ContentReports);
});



// Issue Resolution Evidence (Before/After Photo System)
builder.Services.AddScoped<IRepository<FixIt.Models.Transparency.IssueResolutionEvidence>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Transparency.IssueResolutionEvidence>(db, MongoCollectionNames.IssueResolutionEvidence);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Accessibility.TranslationRecord>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Accessibility.TranslationRecord>(db, MongoCollectionNames.Translations);
});

builder.Services.AddScoped<IRepository<FixIt.Models.Accessibility.SupportedLanguage>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Accessibility.SupportedLanguage>(db, MongoCollectionNames.SupportedLanguages);
});

// Register services (business logic layer)
builder.Services.AddScoped<IIssueService, IssueService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<IReputationService, ReputationService>();
builder.Services.AddScoped<IIssueAnalysisService, OpenAIIssueAnalysisService>();
builder.Services.AddScoped<ICivicAiService, OpenAiCivicAiService>();
builder.Services.AddScoped<FixIt.Services.AI.IAdminSuggestionsService, FixIt.Services.AI.AdminSuggestionsService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IMediaService, MediaService>();

/*
Configure Cloudinary with environment variables priority.
Also resolve placeholder-style expressions in case Railway passes through ${...}.
*/
var cloudinaryCloudName =
    ResolveRailwayStylePlaceholder(Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME"))
    ?? ResolveRailwayStylePlaceholder(builder.Configuration["Cloudinary:CloudName"]);

var cloudinaryApiKey =
    ResolveRailwayStylePlaceholder(Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY"))
    ?? ResolveRailwayStylePlaceholder(builder.Configuration["Cloudinary:ApiKey"]);

var cloudinaryApiSecret =
    ResolveRailwayStylePlaceholder(Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET"))
    ?? ResolveRailwayStylePlaceholder(builder.Configuration["Cloudinary:ApiSecret"]);

/*
Validate Cloudinary placeholders.
If Railway didn't provide the variables and the placeholders come through as-is,
throw a clear error in production (matching existing behavior intent).
*/
var cloudinaryAnyPlaceholder =
    (!string.IsNullOrWhiteSpace(cloudinaryCloudName) && cloudinaryCloudName.Contains("${", StringComparison.Ordinal))
    || (!string.IsNullOrWhiteSpace(cloudinaryApiKey) && cloudinaryApiKey.Contains("${", StringComparison.Ordinal))
    || (!string.IsNullOrWhiteSpace(cloudinaryApiSecret) && cloudinaryApiSecret.Contains("${", StringComparison.Ordinal));

if (cloudinaryAnyPlaceholder && isProduction)
{
    throw new InvalidOperationException(
        "Cloudinary credentials appear to contain placeholder expressions. " +
        "Railway likely did not substitute CLOUDINARY_CLOUD_NAME / CLOUDINARY_API_KEY / CLOUDINARY_API_SECRET. " +
        "Set these environment variables in Railway or configure Cloudinary section in appsettings.json.");
}

// Only register Cloudinary if credentials are provided
if (!string.IsNullOrWhiteSpace(cloudinaryCloudName) && 
    !string.IsNullOrWhiteSpace(cloudinaryApiKey) && 
    !string.IsNullOrWhiteSpace(cloudinaryApiSecret))
{
    // Register Cloudinary as singleton
    builder.Services.AddSingleton(sp =>
    {
        var account = new Account(cloudinaryCloudName, cloudinaryApiKey, cloudinaryApiSecret);
        return new Cloudinary(account);
    });

    // Register CloudinaryService as singleton
    builder.Services.AddSingleton<CloudinaryService>();
}
else
{
    if (isProduction)
    {
        throw new InvalidOperationException(
            "Cloudinary credentials are not configured. Set CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, and CLOUDINARY_API_SECRET environment variables, or configure Cloudinary section in appsettings.json");
    }
    else
    {
        builder.Services.AddScoped<CloudinaryService?>(sp => null);  // Allow null in development
    }
}

builder.Services.AddScoped<IHeatmapService, HeatmapService>();
builder.Services.AddScoped<IHealthReportService, HealthReportService>();
builder.Services.AddScoped<IHazardService, HazardService>();





builder.Services.AddScoped<FixIt.Services.Accessibility.ITranslationService, FixIt.Services.Accessibility.TranslationService>();

// HTTP client for OpenAI
builder.Services.AddHttpClient<IIssueAnalysisService, OpenAIIssueAnalysisService>();
builder.Services.AddHttpClient<ICivicAiService, OpenAiCivicAiService>();

// Register email service
var emailConfig = builder.Configuration.GetSection("Email");
if (emailConfig.GetValue<string>("Provider") == "Smtp" && !string.IsNullOrEmpty(emailConfig["Smtp:Host"]))
{
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, ConsoleEmailService>();
}

// Register audit logging service for compliance and security audits
builder.Services.AddScoped<IAuditService, MongoDbAuditService>();
builder.Services.AddHttpContextAccessor();  // Required for audit service to access request context (IP, user agent)

// Register background services
builder.Services.AddSingleton<IIssueAnalysisQueue, IssueAnalysisQueue>();
builder.Services.AddHostedService(sp => (IssueAnalysisQueue)sp.GetRequiredService<IIssueAnalysisQueue>());
builder.Services.AddHostedService<LeaderboardRegenerationService>();
builder.Services.AddHostedService<HealthReportGenerationService>();

// Configure production-ready CORS
var corsOrigins = StartupConfiguration.ResolveCorsOrigins(
    builder.Configuration,
    ["http://localhost:3000", "http://localhost:5092"]);
if (isProduction && corsOrigins.Length == 0)
{
    throw new InvalidOperationException("At least one CORS allowed origin must be configured in production.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add response compression with Brotli and Gzip
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    // Add Brotli compression (best compression, modern browsers)
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    // Gzip as fallback (broader support)
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    // Specify which MIME types to compress
    options.MimeTypes = new[]
    {
        "text/plain",
        "text/css",
        "text/javascript",
        "text/html",
        "application/json",
        "application/javascript",
        "application/xml",
        "application/xml+rss",
        "application/rss+xml",
        "font/ttf",
        "font/otf",
        "image/svg+xml"
    };
});

// Add distributed/output caching for API responses
builder.Services.AddOutputCache(options =>
{
    // Cache GET requests for health endpoints (30 seconds)
    options.AddPolicy("health-cache", builder =>
    {
        builder.Expire(TimeSpan.FromSeconds(30))
               .SetVaryByRouteValue("controller");
    });
    
    // Cache GET requests for cities/neighborhoods (5 minutes)
    options.AddPolicy("location-cache", builder =>
    {
        builder.Expire(TimeSpan.FromMinutes(5))
               .SetVaryByRouteValue("controller");
    });
    
    // Cache GET requests for tags (10 minutes)
    options.AddPolicy("tag-cache", builder =>
    {
        builder.Expire(TimeSpan.FromMinutes(10))
               .SetVaryByRouteValue("controller");
    });
    
    // Cache public issue summaries (2 minutes)
    options.AddPolicy("issue-summary-cache", builder =>
    {
        builder.Expire(TimeSpan.FromMinutes(2))
               .SetVaryByRouteValue("id");
    });
});

// Add in-memory caching for high-frequency data
builder.Services.AddMemoryCache(options =>
{
    // Set memory cache settings
    options.CompactionPercentage = 0.25;
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
    options.SizeLimit = 100 * 1024 * 1024; // 100 MB max cache size
});

// Add data protection
var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("FixIt");

var dataProtectionKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"]
    ?? builder.Configuration["DATA_PROTECTION_KEY_RING_PATH"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
    var fullPath = Path.GetFullPath(dataProtectionKeyRingPath);
    Directory.CreateDirectory(fullPath);
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(fullPath));
}

var dataProtectionCertificatePath = builder.Configuration["DataProtection:CertificatePath"];
var dataProtectionCertificatePassword = builder.Configuration["DataProtection:CertificatePassword"];
if (!string.IsNullOrWhiteSpace(dataProtectionCertificatePath))
{
    var certificateFullPath = Path.GetFullPath(dataProtectionCertificatePath);
    if (!File.Exists(certificateFullPath))
    {
        throw new InvalidOperationException($"DataProtection certificate file was not found at '{certificateFullPath}'.");
    }

    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
        certificateFullPath,
        string.IsNullOrWhiteSpace(dataProtectionCertificatePassword) ? null : dataProtectionCertificatePassword);
    dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
}

// Configure trusted proxy handling for reverse-proxy deployments
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;

    var trustedProxyList = builder.Configuration["Security:TrustedProxyIps"];
    if (string.IsNullOrWhiteSpace(trustedProxyList))
    {
        return;
    }

    foreach (var proxyCandidate in trustedProxyList.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        if (IPAddress.TryParse(proxyCandidate, out var proxyIp))
        {
            options.KnownProxies.Add(proxyIp);
        }
    }
});

// Add logging with proper configuration for production
builder.Services.AddLogging((config) =>
{
    var env = builder.Environment.IsProduction() ? "Production" : "Development";
    config.ClearProviders();
    
    if (builder.Environment.IsDevelopment())
    {
        config.AddConsole();
        config.AddDebug();
        config.SetMinimumLevel(LogLevel.Information);
    }
    else
    {
        // Production: less verbose logging
        config.AddConsole();
        config.SetMinimumLevel(LogLevel.Warning);
        // Reduce noise from Framework logs
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

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
};

localizationOptions.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.QueryStringRequestCultureProvider());
localizationOptions.RequestCultureProviders.Insert(1, new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());

var app = builder.Build();

if (!hasGoogleClientId || !hasGoogleClientSecret)
{
    if (isProduction)
    {
        // Defensive fallback: production guard is already enforced before build, but keep explicit safety log.
        app.Logger.LogCritical("Google OAuth credentials are missing or invalid in production.");
    }
    else
    {
        app.Logger.LogWarning("Google OAuth is disabled because Authentication:Google credentials are missing or placeholder values.");
    }
}

if (isProduction && string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
    app.Logger.LogWarning("DataProtection:KeyRingPath is not configured. Auth cookies may be invalidated after container restarts.");
}

if (isProduction && string.IsNullOrWhiteSpace(dataProtectionCertificatePath))
{
    app.Logger.LogWarning("DataProtection:CertificatePath is not configured. Data protection keys are persisted without an explicit at-rest encryptor.");
}

// Initialize database (indexes and seed data) - optional if MongoDB is available
try
{
    using (var scope = app.Services.CreateScope())
    {
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        // Only clear database if explicitly requested via environment variable
        // NEVER clear database in production. Only in development with explicit opt-in.
        var shouldResetDb = builder.Configuration["Database:ResetOnStartup"]?.ToLower() == "true" 
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
            
            // Explicitly drop AspNetUsers collection if it exists
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

        // One-time idempotent migration for collection naming consistency.
        await MongoCollectionNamingMigration.RunAsync(mongoContext.Database, logger);
        
        // Seed admin user and other initial data
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        
        try
        {
            // Check if database should be seeded (only on reset or empty)
            int existingUsers = 0;
            try
            {
                existingUsers = userManager.Users.Count();
            }
            catch
            {
                logger.LogInformation("Unable to query users (expected after reset). Proceeding with seeding.");
                existingUsers = 0;
            }
            
            // Only seed initial data if database is empty or reset requested
            if (existingUsers == 0 || shouldResetDb)
            {
                logger.LogInformation("Database is empty or reset requested. Running seeders...");
                await SeederRunner.RunAllConfiguratorsAsync(mongoContext.Database, scope.ServiceProvider);
                logger.LogInformation("Seeder configurations completed.");
                
                // Seed default admin user ONLY when explicitly enabled in development
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
                        logger.LogInformation("Attempting to create development admin user...");
                    
                        // Try to find and remove any existing admin
                        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
                    
                        if (existingAdmin != null)
                        {
                            logger.LogInformation("Found existing admin user. Deleting it to recreate...");
                            var deleteResult = await userManager.DeleteAsync(existingAdmin);
                            if (deleteResult.Succeeded)
                            {
                                logger.LogInformation("Successfully deleted existing admin user.");
                            }
                            else
                            {
                                logger.LogWarning("Failed to delete existing admin user. Errors: {Errors}", 
                                    string.Join("; ", deleteResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
                            }
                        }
                    
                        // Create new admin user
                        var newAdmin = new ApplicationUser
                        {
                            UserName = adminUserName,
                            Email = adminEmail,
                            EmailConfirmed = true,
                            DisplayName = adminDisplayName,
                            Role = UserRole.Admin,
                            TwoFactorEnabled = false
                        };
                        
                        logger.LogInformation("Creating development admin user with email: {AdminEmail}", adminEmail);
                        var result = await userManager.CreateAsync(newAdmin, adminPassword);
                        
                        if (result.Succeeded)
                        {
                            logger.LogInformation("Development admin user created successfully.");
                            logger.LogWarning("Development admin seed is enabled. Disable Database:EnableDevelopmentAdminSeed for non-bootstrap runs.");
                        }
                        else
                        {
                            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                            logger.LogError("Failed to create development admin user. Errors: {Errors}", errors);
                        }
                    }
                }
                else
                {
                    logger.LogInformation("Development admin seed disabled. Skipping admin user bootstrap.");
                }
                
                // Generate leaderboards
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
                
                // Still regenerate leaderboards on startup (can be expensive)
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
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Failed to initialize database. Make sure MongoDB is running at {ConnectionString}", mongoConnectionString);
}

// Configure the HTTP request pipeline.
app.UseMiddleware<FixIt.Middleware.GlobalExceptionHandlingMiddleware>();
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FixIt API v1");
        c.RoutePrefix = "swagger"; // Swagger at /swagger, home page at root
        c.DefaultModelsExpandDepth(-1); // Collapse models by default
    });
}
else
{
    app.UseHsts();
}

// Security headers
app.Use(async (context, next) =>
{
    // Prevent clickjacking
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    // Prevent MIME type sniffing
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    // Enable XSS protection
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    // Referrer policy
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // Restrict high-risk browser capabilities by default
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), payment=()";
    // Content Security Policy
    var csp = securityConfig.GetValue<string>("ContentSecurityPolicy");
    if (!string.IsNullOrEmpty(csp))
    {
        context.Response.Headers["Content-Security-Policy"] = csp;
    }
    await next();
});

app.UseResponseCompression();

// Add middleware for cache control headers and selective ETag support
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
        // Default API behavior is no-store unless an endpoint explicitly opts into caching.
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    }
    else if (context.Request.Headers.ContainsKey("Cookie"))
    {
        // Avoid browser/proxy reuse of cookie-bearing HTML responses.
        context.Response.Headers["Cache-Control"] = "private, no-cache, max-age=0, must-revalidate";
    }

    var isMediaEndpoint =
        path.Contains("/api/media", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/api/uploads", StringComparison.OrdinalIgnoreCase);
    var canBufferForEtag =
        isGetOrHead
        && isApiRequest
        && !isHealthRequest
        && !isMediaEndpoint;

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

var requireHttps = app.Environment.IsProduction() || securityConfig.GetValue<bool>("RequireHttps");
if (app.Environment.IsProduction() && !securityConfig.GetValue<bool>("RequireHttps"))
{
    app.Logger.LogWarning("Security:RequireHttps is false, but HTTPS redirection is enforced in production.");
}

if (requireHttps)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseCors("ProductionCors");

// Add output caching middleware. It must run after routing and CORS for endpoint metadata/policies.
app.UseOutputCache();

if (isRateLimitingEnabled)
{
    app.UseRateLimiter();
}

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Handle 404 Not Found - redirect to custom 404 page
app.UseStatusCodePagesWithReExecute("/404");

// Handle 500 Internal Server Error and other status codes - redirect to error page
app.Use(async (context, next) =>
{
    await next();
    
    // Redirect 5xx errors to the error page
    if (context.Response.StatusCode >= 500 && context.Response.StatusCode < 600)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.Redirect($"/error?code={context.Response.StatusCode}", permanent: false);
        }
    }
});

// Run pending database migrations on startup
// This ensures schema is up-to-date before any requests are processed
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

// Fallback catch-all route for unmatched URLs - redirects to 404
app.MapFallback(context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return context.Response.WriteAsync(string.Empty);
});

// Configure port for Railway deployment
// Priority: PORT environment variable → ASPNETCORE_URLS → default to 8080
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var urls = $"http://0.0.0.0:{port}";
app.Urls.Add(urls);
app.Logger.LogInformation("Application configured to listen on {URLs}", urls);

app.Run();
