using MongoDB.Driver;
using AspNetCore.Identity.Mongo;
using AspNetCore.Identity.Mongo.Model;
using FixIt.Models.Users;
using FixIt.Models.Infrastructure;
using FixIt.Models.Enums;
using FixIt.Models.Gamification;
using FixIt.Data.Infrastructure;
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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Localization configuration
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Add services to the container.
builder.Services.AddRazorPages()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure session for tracking anonymous user views
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
});

// Configure Rate Limiting - Store security config for later use
var securityConfig = builder.Configuration.GetSection("Security");
var rateLimitingConfig = builder.Configuration.GetSection("Security:RateLimiting");
var isRateLimitingEnabled = rateLimitingConfig.GetValue("Enabled", true);

// Configure rate limiting (DDoS protection)
if (isRateLimitingEnabled)
{
    builder.Services.AddRateLimiter(rateLimiterOptions =>
    {
        rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Strict policy for authentication endpoints: 5 attempts per 15 minutes
        rateLimiterOptions.AddFixedWindowLimiter(
            policyName: "auth-strict",
            options =>
            {
                options.PermitLimit = 5;
                options.Window = TimeSpan.FromMinutes(15);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.AutoReplenishment = true;
            });

        // API policy: standard rate limit (60 per minute)
        rateLimiterOptions.AddFixedWindowLimiter(
            policyName: "api",
            options =>
            {
                options.PermitLimit = 60;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.AutoReplenishment = true;
            });

        // Reporting/Analytics: 30 per minute
        rateLimiterOptions.AddFixedWindowLimiter(
            policyName: "reporting",
            options =>
            {
                options.PermitLimit = 30;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.AutoReplenishment = true;
            });

        // Upload policy: stricter for file uploads (10 per minute)
        rateLimiterOptions.AddFixedWindowLimiter(
            policyName: "upload",
            options =>
            {
                options.PermitLimit = 10;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.AutoReplenishment = true;
            });
    });
}

// Configure MongoDB
var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDB connection string not found");
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDB database name not found");

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
    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
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
});

// Add Google OAuth
var googleSection = authConfig.GetSection("Google");
var googleClientId = googleSection["ClientId"];
var googleClientSecret = googleSection["ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });
}
else
{
    throw new InvalidOperationException("Google OAuth credentials are not configured. Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET environment variables.");
}

// Add JWT Bearer authentication for mobile/API clients
// This allows same endpoints to be used by both web (cookies) and mobile (JWT tokens) clients
var jwtConfig = builder.Configuration.GetSection("Jwt");
var jwtSecretKey = jwtConfig["SecretKey"];

if (!string.IsNullOrEmpty(jwtSecretKey))
{
    var key = System.Text.Encoding.ASCII.GetBytes(jwtSecretKey);
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
}

// Register JWT token service for access token and refresh token generation
builder.Services.AddScoped<FixIt.Services.Authentication.ITokenService, FixIt.Services.Authentication.JwtTokenService>();

// Register repositories with correct collection names
builder.Services.AddScoped<IRepository<FixIt.Models.Locations.City>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Locations.City>(db, "cities");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Locations.Neighborhood>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Locations.Neighborhood>(db, "neighborhoods");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Issues.Tag>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Issues.Tag>(db, "tags");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Issues.Issue>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Issues.Issue>(db, "issues");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Engagement.Vote>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Engagement.Vote>(db, "votes");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Issues.ViewEvent>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Issues.ViewEvent>(db, "viewEvents");
});

// Gamification repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Gamification.UserReputation>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Gamification.UserReputation>(db, "userReputations");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Gamification.ReputationTransaction>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Gamification.ReputationTransaction>(db, "reputationTransactions");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Gamification.LeaderboardEntry>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Gamification.LeaderboardEntry>(db, "leaderboards");
});

// Safety (Hazard) repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Safety.Hazard>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Safety.Hazard>(db, "hazards");
});

// User repository for dependency injection
builder.Services.AddScoped<IRepository<FixIt.Models.Users.ApplicationUser>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Users.ApplicationUser>(db, "AspNetUsers");
});

// AI Analysis repositories
builder.Services.AddScoped<IRepository<FixIt.Models.AI.IssueAnalysis>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.AI.IssueAnalysis>(db, "issueAnalyses");
});

// Admin Suggestions repository
builder.Services.AddScoped<IRepository<FixIt.Models.AI.AdminSuggestion>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.AI.AdminSuggestion>(db, "adminSuggestions");
});

// Media repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Media.Media>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Media.Media>(db, "media");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Media.MediaReference>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Media.MediaReference>(db, "mediaReferences");
});

// Engagement repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Engagement.Comment>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Engagement.Comment>(db, "comments");
});

// Moderation repositories
builder.Services.AddScoped<IRepository<FixIt.Models.Moderation.ContentReport>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Moderation.ContentReport>(db, "contentReports");
});



// Issue Resolution Evidence (Before/After Photo System)
builder.Services.AddScoped<IRepository<FixIt.Models.Transparency.IssueResolutionEvidence>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Transparency.IssueResolutionEvidence>(db, "issueResolutionEvidence");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Accessibility.TranslationRecord>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Accessibility.TranslationRecord>(db, "translations");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Accessibility.SupportedLanguage>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Accessibility.SupportedLanguage>(db, "supportedLanguages");
});

// Register services (business logic layer)
builder.Services.AddScoped<IIssueService, IssueService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<IReputationService, ReputationService>();
builder.Services.AddScoped<IIssueAnalysisService, OpenAIIssueAnalysisService>();
builder.Services.AddScoped<FixIt.Services.AI.IAdminSuggestionsService, FixIt.Services.AI.AdminSuggestionsService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IHeatmapService, HeatmapService>();
builder.Services.AddScoped<IHealthReportService, HealthReportService>();
builder.Services.AddScoped<IHazardService, HazardService>();





builder.Services.AddScoped<FixIt.Services.Accessibility.ITranslationService, FixIt.Services.Accessibility.TranslationService>();

// HTTP client for OpenAI
builder.Services.AddHttpClient<IIssueAnalysisService, OpenAIIssueAnalysisService>();

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
builder.Services.AddHostedService<LeaderboardRegenerationService>();
builder.Services.AddHostedService<HealthReportGenerationService>();

// Configure production-ready CORS
var corsConfig = builder.Configuration.GetSection("Security");
var corsOrigins = corsConfig.GetSection("CorsAllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:3000", "http://localhost:5092" };

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
builder.Services.AddDataProtection();

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
                await mongoContext.Database.DropCollectionAsync("AspNetUsers");
                logger.LogInformation("Explicitly dropped AspNetUsers collection.");
            }
            catch
            {
                logger.LogInformation("AspNetUsers collection was already dropped or doesn't exist.");
            }
        }
        
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
                
                // Seed default admin user ONLY in development
                if (app.Environment.IsDevelopment())
                {
                    logger.LogInformation("Attempting to create development admin user...");
                    
                    // Try to find and remove any existing admin
                    var existingAdmin = await userManager.FindByEmailAsync("admin@fixit.local");
                    
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
                        UserName = "admin",
                        Email = "admin@fixit.local",
                        EmailConfirmed = true,
                        DisplayName = "Admin User",
                        Role = UserRole.Admin,
                        TwoFactorEnabled = false
                    };
                    
                    logger.LogInformation("Creating new admin user with email: admin@fixit.local");
                    var tempPassword = "Admin123!";
                    var result = await userManager.CreateAsync(newAdmin, tempPassword);
                    
                    if (result.Succeeded)
                    {
                        logger.LogInformation("✅ Development admin user created successfully.");
                        logger.LogInformation("📧 Email: admin@fixit.local");
                        logger.LogInformation("🔑 Password: Admin123!");
                        logger.LogWarning("⚠️  These are test credentials for development only. Change password before production deployment.");
                    }
                    else
                    {
                        var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                        logger.LogError("❌ Failed to create admin user. Errors: {Errors}", errors);
                    }
                }
                else
                {
                    logger.LogInformation("Production environment - Skipping seed user creation. Manually create admin via identity management.");
                }
                
                // Generate leaderboards
                var reputationService = scope.ServiceProvider.GetRequiredService<IReputationService>();
                await reputationService.RegenerateAllTimeLeaderboardAsync();
                logger.LogInformation("Database seeding and leaderboard generation completed.");
            }
            else
            {
                logger.LogInformation("Database already initialized with {UserCount} users. Skipping seeding.", existingUsers);
                
                // Still regenerate leaderboards on startup (can be expensive)
                var reputationService = scope.ServiceProvider.GetRequiredService<IReputationService>();
                await reputationService.RegenerateAllTimeLeaderboardAsync();
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
    app.UseExceptionHandler("/Error");
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
    // Content Security Policy
    var csp = securityConfig.GetValue<string>("ContentSecurityPolicy");
    if (!string.IsNullOrEmpty(csp))
    {
        context.Response.Headers["Content-Security-Policy"] = csp;
    }
    await next();
});

app.UseResponseCompression();

// Add output caching middleware (must be before routing)
app.UseOutputCache();

// Add middleware for cache control headers
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var method = context.Request.Method;
    
    // Only cache GET and HEAD requests
    if (method != "GET" && method != "HEAD")
    {
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    }
    // Static assets - cache for 1 year
    else if (path.StartsWith("/lib/") || path.StartsWith("/css/") || path.StartsWith("/js/"))
    {
        context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
    }
    // API responses - depend on endpoint caching policy
    else if (path.StartsWith("/api/health"))
    {
        context.Response.Headers["Cache-Control"] = "public, max-age=30";
    }
    else if (path.StartsWith("/api/"))
    {
        // Default API caching: cacheable but revalidate after 60 seconds
        context.Response.Headers["Cache-Control"] = "public, max-age=60, must-revalidate";
    }
    
    // Add ETag support for responses
    var originalBody = context.Response.Body;
    using var memoryStream = new System.IO.MemoryStream();
    context.Response.Body = memoryStream;
    
    await next();
    
    // Skip ETag for non-cacheable responses
    if (context.Response.StatusCode == 200 && 
        (context.Response.ContentType?.Contains("application/json") ?? false))
    {
        memoryStream.Position = 0;
        using var reader = new System.IO.StreamReader(memoryStream);
        var body = await reader.ReadToEndAsync();
        var etag = $"\"{body.GetHashCode():x}\"";
        context.Response.Headers["ETag"] = etag;
        
        // Check If-None-Match header  
        if (context.Request.Headers.TryGetValue("If-None-Match", out var clientEtag) && 
            clientEtag.ToString() == etag)
        {
            context.Response.StatusCode = 304; // Not Modified
            context.Response.ContentLength = 0;
        }
        else
        {
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBody);
        }
    }
    else
    {
        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(originalBody);
    }
});

if (securityConfig.GetValue<bool>("RequireHttps"))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRequestLocalization(localizationOptions);

app.UseCors("ProductionCors");

app.UseRouting();

if (isRateLimitingEnabled)
{
    app.UseRateLimiter();
}

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapAreaControllerRoute(
    name: "Identity",
    areaName: "Identity",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.Run();