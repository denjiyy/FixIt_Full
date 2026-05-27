using FixIt.Services;
using FixIt.Services.AI;
using FixIt.Services.Analytics;
using FixIt.Services.Authentication;
using FixIt.Services.Background;
using FixIt.Services.Contracts;
using FixIt.Services.Email;
using FixIt.Services.Gamification;
using FixIt.Services.Safety;
using FixIt.Services.Storage;

namespace FixIt.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddFixItBusinessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IReputationService, ReputationService>();
        services.AddScoped<IIssueAnalysisService, OpenAIIssueAnalysisService>();
        services.AddScoped<ICivicAiService, OpenAiCivicAiService>();
        services.AddScoped<IAdminSuggestionsService, AdminSuggestionsService>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IMediaService, MediaService>();

        services.AddScoped<IHeatmapService, HeatmapService>();
        services.AddScoped<IHealthReportService, HealthReportService>();
        services.AddScoped<IHazardService, HazardService>();

        // Typed HTTP clients for the AI services.
        services.AddHttpClient<IIssueAnalysisService, OpenAIIssueAnalysisService>();
        services.AddHttpClient<ICivicAiService, OpenAiCivicAiService>();

        // Named HTTP client used by GeocodingController for Nominatim reverse geocoding.
        services.AddHttpClient(FixIt.Controllers.GeocodingController.NominatimHttpClientName, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FixIt-HazardReporting-App/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        var emailConfig = configuration.GetSection("Email");
        if (string.Equals(emailConfig.GetValue<string>("Provider"), "Smtp", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(emailConfig["Smtp:Host"]))
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, ConsoleEmailService>();
        }

        services.AddScoped<IAuditService, MongoDbAuditService>();
        services.AddHttpContextAccessor();

        services.AddSingleton<IIssueAnalysisQueue, IssueAnalysisQueue>();
        services.AddHostedService(sp => (IssueAnalysisQueue)sp.GetRequiredService<IIssueAnalysisQueue>());
        services.AddHostedService<LeaderboardRegenerationService>();
        services.AddHostedService<HealthReportGenerationService>();

        return services;
    }
}
