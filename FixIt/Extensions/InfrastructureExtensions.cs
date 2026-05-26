using System.Security.Cryptography.X509Certificates;
using CloudinaryDotNet;
using FixIt.Configuration;
using FixIt.Services;
using FixIt.Services.Constants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using System.Threading.RateLimiting;

namespace FixIt.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddFixItRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetSection("Security:RateLimiting").GetValue("Enabled", true);
        if (!enabled)
        {
            return services;
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("PerClientKey", context => FixedWindowFor(context, permits: 60, TimeSpan.FromMinutes(1)));
            options.AddPolicy(RateLimitPolicyNames.AuthStrict, context => FixedWindowFor(context, permits: 5, TimeSpan.FromMinutes(15)));
            options.AddPolicy(RateLimitPolicyNames.Api, context => FixedWindowFor(context, permits: 60, TimeSpan.FromMinutes(1)));
            options.AddPolicy(RateLimitPolicyNames.Reporting, context => FixedWindowFor(context, permits: 30, TimeSpan.FromMinutes(1)));
            options.AddPolicy(RateLimitPolicyNames.Upload, context => FixedWindowFor(context, permits: 10, TimeSpan.FromMinutes(1)));
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindowFor(HttpContext context, int permits, TimeSpan window)
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var clientKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    public static IServiceCollection AddFixItCloudinary(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var isProduction = environment.IsProduction();

        var cloudName = StartupConfigurationHelpers.ResolveRailwayStylePlaceholder(
            Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME"))
            ?? StartupConfigurationHelpers.ResolveRailwayStylePlaceholder(configuration["Cloudinary:CloudName"]);

        var apiKey = StartupConfigurationHelpers.ResolveRailwayStylePlaceholder(
            Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY"))
            ?? StartupConfigurationHelpers.ResolveRailwayStylePlaceholder(configuration["Cloudinary:ApiKey"]);

        var apiSecret = StartupConfigurationHelpers.ResolveRailwayStylePlaceholder(
            Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET"))
            ?? StartupConfigurationHelpers.ResolveRailwayStylePlaceholder(configuration["Cloudinary:ApiSecret"]);

        var anyPlaceholder =
            (!string.IsNullOrWhiteSpace(cloudName) && cloudName.Contains("${", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(apiKey) && apiKey.Contains("${", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(apiSecret) && apiSecret.Contains("${", StringComparison.Ordinal));

        if (anyPlaceholder && isProduction)
        {
            throw new InvalidOperationException(
                "Cloudinary credentials appear to contain placeholder expressions. " +
                "Railway likely did not substitute CLOUDINARY_CLOUD_NAME / CLOUDINARY_API_KEY / CLOUDINARY_API_SECRET. " +
                "Set these environment variables in Railway or configure Cloudinary section in appsettings.json.");
        }

        if (!string.IsNullOrWhiteSpace(cloudName)
            && !string.IsNullOrWhiteSpace(apiKey)
            && !string.IsNullOrWhiteSpace(apiSecret))
        {
            services.AddSingleton(_ => new Cloudinary(new Account(cloudName, apiKey, apiSecret)));
            services.AddSingleton<CloudinaryService>();
        }
        else if (isProduction)
        {
            throw new InvalidOperationException(
                "Cloudinary credentials are not configured. Set CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, and CLOUDINARY_API_SECRET environment variables, or configure Cloudinary section in appsettings.json");
        }
        else
        {
            services.AddScoped<CloudinaryService>(_ => null!);
        }

        return services;
    }

    public static IServiceCollection AddFixItCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var corsOrigins = StartupConfiguration.ResolveCorsOrigins(
            configuration,
            ["http://localhost:3000", "http://localhost:5092"]);

        if (environment.IsProduction() && corsOrigins.Length == 0)
        {
            throw new InvalidOperationException("At least one CORS allowed origin must be configured in production.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy("ProductionCors", policy =>
            {
                policy.WithOrigins(corsOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddFixItCachingAndCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
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

        services.AddOutputCache(options =>
        {
            options.AddPolicy("health-cache", b => b.Expire(TimeSpan.FromSeconds(30)).SetVaryByRouteValue("controller"));
            options.AddPolicy("location-cache", b => b.Expire(TimeSpan.FromMinutes(5)).SetVaryByRouteValue("controller"));
            options.AddPolicy("tag-cache", b => b.Expire(TimeSpan.FromMinutes(10)).SetVaryByRouteValue("controller"));
            options.AddPolicy("issue-summary-cache", b => b.Expire(TimeSpan.FromMinutes(2)).SetVaryByRouteValue("id"));
        });

        services.AddMemoryCache(options =>
        {
            options.CompactionPercentage = 0.25;
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
            options.SizeLimit = 100 * 1024 * 1024;
        });

        return services;
    }

    public static IServiceCollection AddFixItDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var builder = services.AddDataProtection().SetApplicationName("FixIt");

        var keyRingPath = configuration["DataProtection:KeyRingPath"]
            ?? configuration["DATA_PROTECTION_KEY_RING_PATH"];
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            var fullPath = Path.GetFullPath(keyRingPath);
            Directory.CreateDirectory(fullPath);
            builder.PersistKeysToFileSystem(new DirectoryInfo(fullPath));
        }

        var certPath = configuration["DataProtection:CertificatePath"];
        var certPassword = configuration["DataProtection:CertificatePassword"];
        if (!string.IsNullOrWhiteSpace(certPath))
        {
            var certificateFullPath = Path.GetFullPath(certPath);
            if (!File.Exists(certificateFullPath))
            {
                throw new InvalidOperationException($"DataProtection certificate file was not found at '{certificateFullPath}'.");
            }

            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificateFullPath,
                string.IsNullOrWhiteSpace(certPassword) ? null : certPassword);
            builder.ProtectKeysWithCertificate(certificate);
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
