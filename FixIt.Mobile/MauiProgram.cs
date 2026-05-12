using CommunityToolkit.Maui;
using FixIt.Mobile.Services;
using FixIt.Mobile.ViewModels;
using FixIt.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;
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

        builder.Services.AddHttpClient("FixItApi", client =>
        {
            client.BaseAddress = new Uri("https://fixit-production-202d.up.railway.app/");
        });

        builder.Services.AddSingleton<ApiService>();

        builder.Services.AddTransient<AppShell>();

        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<IssuesViewModel>();
        builder.Services.AddTransient<ReportIssueViewModel>();
        builder.Services.AddTransient<LoginViewModel>();

        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<IssuesPage>();
        builder.Services.AddTransient<ReportIssuePage>();
        builder.Services.AddTransient<LoginPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
