using MongoDB.Driver;
using AspNetCore.Identity.Mongo;
using AspNetCore.Identity.Mongo.Model;
using FixIt.Models.Users;
using FixIt.Models.Infrastructure;
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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure MongoDB
var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDB connection string not found");
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDB database name not found");

// Register MongoClient as singleton
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString(mongoConnectionString);
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
},
mongo =>
{
    mongo.ConnectionString = mongoConnectionString;
});

// Configure OAuth Authentication
var authConfig = builder.Configuration.GetSection("Authentication");
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
});

// Only add Google if credentials are configured
var googleClientId = authConfig["Google:ClientId"];
var googleClientSecret = authConfig["Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !googleClientId.Contains("YOUR_") && 
    !string.IsNullOrEmpty(googleClientSecret) && !googleClientSecret.Contains("YOUR_"))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });
}

// Only add Microsoft if credentials are configured
var microsoftClientId = authConfig["Microsoft:ClientId"];
var microsoftClientSecret = authConfig["Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(microsoftClientId) && !microsoftClientId.Contains("YOUR_") && 
    !string.IsNullOrEmpty(microsoftClientSecret) && !microsoftClientSecret.Contains("YOUR_"))
{
    builder.Services.AddAuthentication().AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;
        options.CallbackPath = "/signin-microsoft";
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
    });
}

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

// Register services (business logic layer)
builder.Services.AddScoped<IIssueService, IssueService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<IReputationService, ReputationService>();
builder.Services.AddScoped<IIssueAnalysisService, OpenAIIssueAnalysisService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IHeatmapService, HeatmapService>();
builder.Services.AddScoped<IHealthReportService, HealthReportService>();
builder.Services.AddScoped<IHazardService, HazardService>();

// HTTP client for OpenAI
builder.Services.AddHttpClient<IIssueAnalysisService, OpenAIIssueAnalysisService>();

// Register background services
builder.Services.AddHostedService<LeaderboardRegenerationService>();

// Configure production-ready CORS
var securityConfig = builder.Configuration.GetSection("Security");
var corsOrigins = securityConfig.GetSection("CorsAllowedOrigins").Get<string[]>() 
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

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Add data protection
builder.Services.AddDataProtection();

// Add logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

var app = builder.Build();

// Initialize database (indexes and seed data) - optional if MongoDB is available
try
{
    using (var scope = app.Services.CreateScope())
    {
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        await SeederRunner.RunAllConfiguratorsAsync(mongoContext.Database, scope.ServiceProvider);
        
        // Generate AllTime leaderboard on startup
        var reputationService = scope.ServiceProvider.GetRequiredService<IReputationService>();
        await reputationService.RegenerateAllTimeLeaderboardAsync();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("AllTime leaderboard generated on application startup");
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
    context.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
    // Prevent MIME type sniffing
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    // Enable XSS protection
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    // Referrer policy
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    // Content Security Policy
    var csp = securityConfig.GetValue<string>("ContentSecurityPolicy");
    if (!string.IsNullOrEmpty(csp))
    {
        context.Response.Headers.Add("Content-Security-Policy", csp);
    }
    await next();
});

app.UseResponseCompression();

if (securityConfig.GetValue<bool>("RequireHttps"))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseCors("ProductionCors");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapAreaControllerRoute(
    name: "Identity",
    areaName: "Identity",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.Run();