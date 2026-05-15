using FixIt.Mobile.Localization;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile;

public partial class App : Application
{
    private readonly IAnalyticsService _analytics;
    private readonly AppShell _appShell;

    public App(AppShell appShell, IAnalyticsService analytics)
    {
        InitializeComponent();
        _appShell = appShell;
        _analytics = analytics;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_appShell);
    }

    private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new InvalidOperationException("Unhandled application error");
        await _analytics.TrackError(exception);
        await ShowGenericErrorAsync();
    }

    private async void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        await _analytics.TrackError(e.Exception);
        await ShowGenericErrorAsync();
    }

    private static Task ShowGenericErrorAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlert(
                    LocalizationService.Get("Common_Error_Generic"),
                    LocalizationService.Get("Common_Error_Generic"),
                    LocalizationService.Get("Common_OK"));
            }
        });
    }
}
