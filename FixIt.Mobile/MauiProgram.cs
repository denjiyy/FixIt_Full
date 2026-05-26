using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CommunityToolkit.Maui;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;
using FixIt.Mobile.ViewModels;
using FixIt.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace FixIt.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(_ => new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = JsonTypeInfoResolver.Combine(FixItJsonContext.Default, new DefaultJsonTypeInfoResolver())
        });

        builder.Services.AddSingleton(LocalizationService.Instance);
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<IAnalyticsService, ConsoleAnalyticsService>();
        builder.Services.AddSingleton<IPerformanceService, ConsolePerformanceService>();

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        builder.Services.AddTransient<AuthHeaderHandler>();

        builder.Services.AddHttpClient(AppConstants.AuthClientName, client =>
            {
                client.BaseAddress = new Uri(AppConstants.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(MobileSettings.ApiTimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

        builder.Services.AddHttpClient(AppConstants.ApiClientName, client =>
            {
                client.BaseAddress = new Uri(AppConstants.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(MobileSettings.ApiTimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            })
            .AddHttpMessageHandler<AuthHeaderHandler>();

        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<IApiService>(sp => sp.GetRequiredService<ApiService>());
        builder.Services.AddSingleton<ShellViewModel>();

        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddSingleton<IssuesViewModel>();
        builder.Services.AddSingleton<AlertsViewModel>();
        builder.Services.AddSingleton<ReportIssueViewModel>();
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddSingleton<ProfileViewModel>();
        builder.Services.AddTransient<IssueDetailViewModel>();
        builder.Services.AddTransient<MyIssuesViewModel>();
        builder.Services.AddTransient<HazardMapViewModel>();
        builder.Services.AddTransient<LeaderboardViewModel>();
        builder.Services.AddTransient<HealthReportViewModel>();
        builder.Services.AddTransient<PublicProfileViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<IssuesPage>();
        builder.Services.AddSingleton<AlertsPage>();
        builder.Services.AddSingleton<ReportIssuePage>();
        builder.Services.AddSingleton<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddSingleton<ProfilePage>();
        builder.Services.AddTransient<IssueDetailPage>();
        builder.Services.AddTransient<MyIssuesPage>();
        builder.Services.AddTransient<HazardMapPage>();
        builder.Services.AddTransient<LeaderboardPage>();
        builder.Services.AddTransient<HealthReportPage>();
        builder.Services.AddTransient<PublicProfilePage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
