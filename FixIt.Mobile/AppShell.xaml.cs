using System.ComponentModel;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Services.Contracts;
using FixIt.Mobile.ViewModels;
using FixIt.Mobile.Views;

namespace FixIt.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthService _auth;
    private readonly LoginPage _loginPage;
    private readonly ProfilePage _profilePage;
    private readonly ShellViewModel _viewModel;
    private bool _isRedirecting;

    public AppShell(
        HomePage homePage,
        IssuesPage issuesPage,
        AlertsPage alertsPage,
        ReportIssuePage reportIssuePage,
        LoginPage loginPage,
        ProfilePage profilePage,
        ShellViewModel viewModel,
        IAuthService auth)
    {
        _auth = auth;
        _viewModel = viewModel;
        _loginPage = loginPage;
        _profilePage = profilePage;

        InitializeComponent();

        BindingContext = _viewModel;

        HomeContent.Content = homePage;
        IssuesContent.Content = issuesPage;
        AlertsContent.Content = alertsPage;
        ReportIssueContent.Content = reportIssuePage;

        Routing.RegisterRoute(AppConstants.RouteIssueDetail, typeof(IssueDetailPage));
        Routing.RegisterRoute(AppConstants.RouteMyIssues, typeof(MyIssuesPage));
        Routing.RegisterRoute(AppConstants.RouteHazardMap, typeof(HazardMapPage));
        Routing.RegisterRoute(AppConstants.RouteLeaderboard, typeof(LeaderboardPage));
        Routing.RegisterRoute(AppConstants.RouteHealthReport, typeof(HealthReportPage));
        Routing.RegisterRoute(AppConstants.RoutePublicProfile, typeof(PublicProfilePage));
        // FIX B-01: keep the raw sign-in tab route registered and use the absolute route only for Shell tab selection.
        Routing.RegisterRoute(AppConstants.RouteSignInTab, typeof(LoginPage));
        Routing.RegisterRoute("register", typeof(RegisterPage));

        UpdateAuthVisualState(_auth.IsLoggedIn);
        UpdateLocalizedTabs();
        _auth.LoginStateChanged += OnLoginStateChanged;
        LocalizationService.CultureChanged += OnCultureChanged;
        _viewModel.PropertyChanged += OnShellViewModelPropertyChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        if (_auth is null || _isRedirecting || _auth.IsLoggedIn)
            return;

        if (args.Target?.Location is null)
            return;

        var targetRoute = args.Target.Location.OriginalString;
        if (targetRoute.Contains("report-issue-tab", StringComparison.OrdinalIgnoreCase))
        {
            args.Cancel();
            _ = RedirectToSignInAsync();
        }
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        await _auth.InitializeAsync();
        UpdateAuthVisualState(_auth.IsLoggedIn);
        await AnimateOfflineBannerAsync(_viewModel.IsOffline);

        if (!_auth.IsLoggedIn)
        {
            await RedirectToSignInAsync();
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _auth.LoginStateChanged -= OnLoginStateChanged;
        LocalizationService.CultureChanged -= OnCultureChanged;
        _viewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
    }

    private async Task RedirectToSignInAsync()
    {
        if (_isRedirecting)
            return;

        _isRedirecting = true;
        try
        {
            await GoToAsync(AppConstants.RouteSignInTabAbsolute);
        }
        finally
        {
            _isRedirecting = false;
        }
    }

    private async void OnLoginStateChanged(object? sender, bool isLoggedIn)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            UpdateAuthVisualState(isLoggedIn);

            if (isLoggedIn)
                await GoToAsync(AppConstants.RouteHome);
            else
                await GoToAsync(AppConstants.RouteSignInTabAbsolute);
        });
    }

    private void UpdateAuthVisualState(bool isLoggedIn)
    {
        AccountContent.Content = isLoggedIn ? _profilePage : _loginPage;
        UpdateLocalizedTabs();
    }

    private void UpdateLocalizedTabs()
    {
        HomeTab.Title = $"🏠 {LocalizationService.Get("Tab_Home")}";
        IssuesTab.Title = $"📋 {LocalizationService.Get("Tab_Issues")}";
        AlertsTab.Title = $"🔔 {LocalizationService.Get("Tab_Alerts")}";
        ReportIssueTab.Title = _auth.IsLoggedIn
            ? $"📷 {LocalizationService.Get("Tab_Report")}"
            : $"🔒 {LocalizationService.Get("Tab_ReportLocked")}";
        AccountTab.Title = _auth.IsLoggedIn
            ? $"{_auth.GetCurrentInitials()} {LocalizationService.Get("Tab_Profile")}"
            : $"👤 {LocalizationService.Get("Tab_SignIn")}";
    }

    private void OnCultureChanged(object? sender, System.Globalization.CultureInfo e)
    {
        UpdateLocalizedTabs();
    }

    private async void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsOffline))
            await AnimateOfflineBannerAsync(_viewModel.IsOffline);
    }

    private async Task AnimateOfflineBannerAsync(bool isOffline)
    {
        if (isOffline)
        {
            OfflineBanner.IsVisible = true;
            await OfflineBanner.FadeTo(1, 250, Easing.CubicOut);
        }
        else
        {
            await OfflineBanner.FadeTo(0, 250, Easing.CubicOut);
            OfflineBanner.IsVisible = false;
        }
    }
}
