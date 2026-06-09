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
        LocalizationService.CultureChanged += OnCultureChanged;
        App.Resumed += OnAppResumed;
    }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    // Issue "posts" rendered as the main feed.
    public ObservableCollection<Issue> FeedIssues { get; } = [];

    public async Task OnAppearingAsync()
    {
        SubscribeAuth();
        await _analytics.TrackScreen("Home");
        await LoadFeedAsync(false, _cts.Token);
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
        UnsubscribeAuth();
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        await LoadFeedAsync(true, ct);
    }

    [RelayCommand]
    private async Task UpvoteAsync(Issue? issue, CancellationToken ct)
    {
        if (issue is null)
        {
            return;
        }

        HapticService.Click();
        var upvote = !issue.UserHasUpvoted;

        // Optimistic update, then reconcile with the server.
        ApplyVote(issue, upvote);
        try
        {
            var result = await _api.VoteAsync(issue.Id, upvote, ct);
            if (!result.Success)
            {
                ApplyVote(issue, !upvote);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ApplyVote(issue, !upvote);
            await _analytics.TrackError(ex);
        }
    }

    [RelayCommand]
    private void Save(Issue? issue)
    {
        if (issue is null)
        {
            return;
        }

        HapticService.Click();
        issue.IsSaved = !issue.IsSaved;
        RefreshItem(issue);
    }

    [RelayCommand]
    private async Task ShareAsync(Issue? issue)
    {
        if (issue is null)
        {
            return;
        }

        HapticService.Click();
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = issue.Title,
                Text = $"{issue.Title} — {issue.PrimaryLocation}"
            });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
    }

    [RelayCommand]
    private async Task OpenProfileAsync(Issue? issue, CancellationToken ct)
    {
        if (issue is null || string.IsNullOrWhiteSpace(issue.AuthorUserId))
        {
            return;
        }

        HapticService.Click();
        await Shell.Current.GoToAsync($"{AppConstants.RoutePublicProfile}?UserId={Uri.EscapeDataString(issue.AuthorUserId)}");
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
    private async Task GoToHazardMapAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteHazardMode);
    }

    [RelayCommand]
    private async Task GoToHealthReportAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteHealthReport);
    }

    [RelayCommand]
    private async Task GoToLeaderboardAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteLeaderboard);
    }

    private async Task LoadFeedAsync(bool forceRefresh, CancellationToken ct)
    {
        if (IsLoading)
        {
            return;
        }

        if (!forceRefresh && FeedIssues.Count > 0 && DateTime.UtcNow - _lastLoaded < TimeSpan.FromMinutes(MobileSettings.DataCacheMinutes))
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

                FeedIssues.Clear();
                foreach (var issue in issues)
                {
                    FeedIssues.Add(issue);
                }

                IsEmpty = FeedIssues.Count == 0;
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

    private void ApplyVote(Issue issue, bool upvote)
    {
        if (issue.UserHasUpvoted == upvote)
        {
            return;
        }

        issue.UserHasUpvoted = upvote;
        issue.VoteCount = Math.Max(0, issue.VoteCount + (upvote ? 1 : -1));
        RefreshItem(issue);
    }

    private void RefreshItem(Issue issue)
    {
        var index = FeedIssues.IndexOf(issue);
        if (index >= 0)
        {
            // Re-set the slot so the CollectionView rebuilds the row (Issue is a plain DTO).
            FeedIssues[index] = issue;
        }
    }

    private void OnLoginStateChanged(object? sender, bool isLoggedIn)
    {
        IsLoggedIn = isLoggedIn;
    }

    private void OnCultureChanged(object? sender, System.Globalization.CultureInfo e)
    {
        Title = LocalizationService.Get("Home_Title");
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
