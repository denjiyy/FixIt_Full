using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class HomeViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private DateTime _lastLoaded = DateTime.MinValue;
    private bool _disposed;
    private bool _subscribed;

    public HomeViewModel(IApiService api, IAuthService auth, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _auth = auth;
        _analytics = analytics;
        _performance = performance;
        SubscribeAuth();
        IsLoggedIn = _auth.IsLoggedIn;
        Title = LocalizationService.Get("Home_Title");
        WelcomeMessage = LocalizationService.Get("Home_Subtitle");
        LocalizationService.CultureChanged += OnCultureChanged;
        App.Resumed += OnAppResumed;
    }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _welcomeMessage = string.Empty;

    [ObservableProperty]
    private int _totalIssues;

    [ObservableProperty]
    private int _resolvedIssues;

    [ObservableProperty]
    private int _inProgressIssues;

    [ObservableProperty]
    private int _criticalIssues;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    public ObservableCollection<Issue> RecentIssues { get; } = [];

    public async Task OnAppearingAsync()
    {
        SubscribeAuth();
        await _analytics.TrackScreen("Home");
        await LoadDashboardAsync(false, _cts.Token);
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
        UnsubscribeAuth();
    }

    [RelayCommand]
    private async Task LoadDashboardAsync(CancellationToken ct)
    {
        await LoadDashboardAsync(false, ct);
    }

    [RelayCommand]
    private async Task ReportIssueAsync(CancellationToken ct)
    {
        HapticService.Click();
        if (_auth.IsLoggedIn)
        {
            await Shell.Current.GoToAsync(AppConstants.RouteReportIssueTab);
            return;
        }

        await Shell.Current.GoToAsync(AppConstants.RouteAccountTabAbsolute);
    }

    [RelayCommand]
    private async Task GoToIssuesAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteIssues);
    }

    [RelayCommand]
    private async Task GoToHealthReportAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteHealthReport);
    }

    [RelayCommand]
    private async Task GoToHazardMapAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteHazardMap);
    }

    [RelayCommand]
    private async Task NavigateToIssueAsync(string? issueId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issueId))
        {
            return;
        }

        HapticService.Click();
        await Shell.Current.GoToAsync($"{AppConstants.RouteIssueDetail}?IssueId={Uri.EscapeDataString(issueId)}");
    }

    [RelayCommand]
    private async Task GoToLeaderboardAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteLeaderboard);
    }

    private async Task LoadDashboardAsync(bool forceRefresh, CancellationToken ct)
    {
        if (IsLoading)
        {
            return;
        }

        if (!forceRefresh && RecentIssues.Count > 0 && DateTime.UtcNow - _lastLoaded < TimeSpan.FromMinutes(MobileSettings.DataCacheMinutes))
        {
            return;
        }

        try
        {
            IsLoading = true;
            using (_performance.StartTrace("LoadHome"))
            {
                var issues = await _api.GetIssuesAsync(page: 1, pageSize: 50, ct: ct);
                ct.ThrowIfCancellationRequested();

                TotalIssues = issues.Count;
                ResolvedIssues = issues.Count(i => i.Status == AppConstants.StatusResolved);
                InProgressIssues = issues.Count(i => i.Status == AppConstants.StatusInProgress);
                CriticalIssues = issues.Count(i => i.Status == AppConstants.StatusCritical);

                RecentIssues.Clear();
                foreach (var issue in issues.Take(5))
                {
                    RecentIssues.Add(issue);
                }

                IsEmpty = RecentIssues.Count == 0;
                _lastLoaded = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "operation_cancelled" });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnLoginStateChanged(object? sender, bool isLoggedIn)
    {
        IsLoggedIn = isLoggedIn;
    }

    private void OnCultureChanged(object? sender, System.Globalization.CultureInfo e)
    {
        Title = LocalizationService.Get("Home_Title");
        WelcomeMessage = LocalizationService.Get("Home_Subtitle");
    }

    private void OnAppResumed(object? sender, EventArgs e)
    {
        // FIX B-06: invalidate dashboard cache after app suspension so the next appearance refreshes data.
        _lastLoaded = DateTime.MinValue;
    }

    private void SubscribeAuth()
    {
        if (_subscribed)
        {
            return;
        }

        _auth.LoginStateChanged += OnLoginStateChanged;
        _subscribed = true;
    }

    private void UnsubscribeAuth()
    {
        if (!_subscribed)
        {
            return;
        }

        _auth.LoginStateChanged -= OnLoginStateChanged;
        _subscribed = false;
    }

    private void CancelAndRenew()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnsubscribeAuth();
        LocalizationService.CultureChanged -= OnCultureChanged;
        App.Resumed -= OnAppResumed;
        _cts.Cancel();
        _cts.Dispose();
    }
}
