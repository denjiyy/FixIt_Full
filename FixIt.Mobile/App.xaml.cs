using FixIt.Mobile.Localization;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile;

public partial class App : Application
{
    private readonly IAnalyticsService _analytics;
    private readonly IServiceProvider _serviceProvider;
    private bool _connectivityDisposed;

    public static event EventHandler? Resumed;

    public App(IServiceProvider serviceProvider, IAnalyticsService analytics)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _analytics = analytics;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_serviceProvider.GetRequiredService<AppShell>());
        window.Destroying += OnWindowDestroying;
        return window;
    }

    protected override void OnResume()
    {
        base.OnResume();
        Resumed?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowDestroying(object? sender, EventArgs e)
    {
        if (_connectivityDisposed)
        {
            return;
        }

        _connectivityDisposed = true;
        // FIX B-08: dispose the singleton connectivity subscription when the app window is destroyed.
        if (_serviceProvider.GetService<IConnectivityService>() is IDisposable disposable)
        {
            disposable.Dispose();
        }
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
