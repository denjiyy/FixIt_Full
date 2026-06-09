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
        Routing.RegisterRoute(AppConstants.RouteLeaderboard, typeof(LeaderboardPage));
        Routing.RegisterRoute(AppConstants.RouteHealthReport, typeof(HealthReportPage));
        Routing.RegisterRoute(AppConstants.RoutePublicProfile, typeof(PublicProfilePage));
        Routing.RegisterRoute(AppConstants.RouteSettings, typeof(SettingsPage));
        Routing.RegisterRoute(AppConstants.RouteEditIssue, typeof(EditIssuePage));
        Routing.RegisterRoute(AppConstants.RouteForgotPassword, typeof(ForgotPasswordPage));
        Routing.RegisterRoute(AppConstants.RouteCities, typeof(CitiesPage));
        Routing.RegisterRoute(AppConstants.RouteHeatmap, typeof(HeatmapPage));
        Routing.RegisterRoute(AppConstants.RouteTagDetail, typeof(TagDetailPage));
        Routing.RegisterRoute(AppConstants.RouteHazardMode, typeof(HazardModePage));
        Routing.RegisterRoute(AppConstants.RouteConnectedAccounts, typeof(ConnectedAccountsPage));
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
            await GoToAsync(AppConstants.RouteAccountTabAbsolute);
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
                await GoToAsync(AppConstants.RouteAccountTabAbsolute);
        });
    }

    private void UpdateAuthVisualState(bool isLoggedIn)
    {
        AccountContent.Content = isLoggedIn ? _profilePage : _loginPage;
        UpdateLocalizedTabs();
        UpdateTabIcons();
    }

    private void UpdateLocalizedTabs()
    {
        HomeTab.Title = LocalizationService.Get("Tab_Home");
        IssuesTab.Title = LocalizationService.Get("Tab_Issues");
        AlertsTab.Title = LocalizationService.Get("Tab_Alerts");
        ReportIssueTab.Title = _auth.IsLoggedIn
            ? LocalizationService.Get("Tab_Report")
            : LocalizationService.Get("Tab_ReportLocked");
        AccountTab.Title = _auth.IsLoggedIn
            ? LocalizationService.Get("Tab_Profile")
            : LocalizationService.Get("Tab_SignIn");
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        UpdateTabIcons();
    }

    private void UpdateTabIcons()
    {
        SetTabIcon(HomeTab, HomeContent, TabIconFile("home", HomeContent));
        SetTabIcon(IssuesTab, IssuesContent, TabIconFile("issues", IssuesContent));
        SetTabIcon(AlertsTab, AlertsContent, TabIconFile("alerts", AlertsContent));
        // Centre tab uses a fixed elevated FAB-style icon; it does not flip between
        // selected/unselected variants because the icon is the same in both states.
        SetTabIcon(
            ReportIssueTab,
            ReportIssueContent,
            _auth.IsLoggedIn ? "report_fab.png" : "report_fab_locked.png");
        SetTabIcon(AccountTab, AccountContent, TabIconFile("profile", AccountContent));
    }

    private static void SetTabIcon(ShellSection tab, ShellContent content, string iconFile)
    {
        var icon = ImageSource.FromFile(iconFile);
        tab.Icon = icon;
        content.Icon = icon;
    }

    private string TabIconFile(string name, ShellContent content)
    {
        var suffix = IsCurrentContent(content) ? "_selected" : string.Empty;
        return $"{name}{suffix}.png";
    }

    private bool IsCurrentContent(ShellContent content)
    {
        return CurrentItem?.CurrentItem?.CurrentItem == content;
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
