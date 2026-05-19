using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class IssuesViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _searchDebounceCts;
    private DateTime _lastLoaded = DateTime.MinValue;
    private int _currentPage = 1;
    private bool _hasMore = true;
    private bool _disposed;

    public IssuesViewModel(IApiService api, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _analytics = analytics;
        _performance = performance;
    }

    public ObservableCollection<Issue> Issues { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _naturalLanguageQuery = string.Empty;

    [ObservableProperty]
    private string _activeFilter = AppConstants.FilterAll;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private bool _isApplyingNaturalLanguageFilter;

    [ObservableProperty]
    private bool _isEmpty;

    public bool IsAllFilterActive => ActiveFilter == AppConstants.FilterAll;

    public bool IsNewFilterActive => ActiveFilter == AppConstants.FilterNew;

    public bool IsInProgressFilterActive => ActiveFilter == AppConstants.FilterInProgress;

    public bool IsResolvedFilterActive => ActiveFilter == AppConstants.FilterResolved;

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("Issues");
        if (Issues.Count == 0)
        {
            await LoadIssuesInternalAsync(false, _cts.Token);
        }
    }

    public void OnDisappearing()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;
        CancelAndRenew();
    }

    partial void OnActiveFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsNewFilterActive));
        OnPropertyChanged(nameof(IsInProgressFilterActive));
        OnPropertyChanged(nameof(IsResolvedFilterActive));
    }

    partial void OnSearchTextChanged(string value)
    {
        DebounceSearch();
    }

    [RelayCommand]
    private async Task LoadIssuesAsync(CancellationToken ct)
    {
        await LoadIssuesInternalAsync(false, ct);
    }

    [RelayCommand]
    private async Task RefreshIssuesAsync(CancellationToken ct)
    {
        await LoadIssuesInternalAsync(true, ct);
    }

    [RelayCommand]
    private async Task LoadMoreIssuesAsync(CancellationToken ct)
    {
        if (IsLoading || IsLoadingMore || !_hasMore)
        {
            return;
        }

        try
        {
            IsLoadingMore = true;
            using (_performance.StartTrace("LoadIssuesMore"))
            {
                var nextPage = _currentPage + 1;
                var filter = ActiveFilter == AppConstants.FilterAll ? null : ActiveFilter;
                var issues = await _api.GetIssuesAsync(filter, SearchText, nextPage, MobileSettings.PaginationPageSize, ct);
                ct.ThrowIfCancellationRequested();

                foreach (var issue in issues)
                {
                    Issues.Add(issue);
                }

                _currentPage = nextPage;
                _hasMore = issues.Count >= MobileSettings.PaginationPageSize;
                IsEmpty = Issues.Count == 0;
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
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task SetFilterAsync(string? filter, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return;
        }

        HapticService.Click();
        ActiveFilter = filter;
        await LoadIssuesInternalAsync(true, ct);
    }

    [RelayCommand]
    private async Task ApplyNaturalLanguageFilterAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NaturalLanguageQuery))
        {
            return;
        }

        HapticService.Click();
        try
        {
            IsApplyingNaturalLanguageFilter = true;
            var result = await _api.TranslateNaturalLanguageFilterAsync(NaturalLanguageQuery, ct);
            SearchText = result?.SearchQuery ?? NaturalLanguageQuery;
            ActiveFilter = MapStatusToFilter(result?.Status);
            await LoadIssuesInternalAsync(true, ct);
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "ai_filter_cancelled" });
        }
        catch (Exception ex)
        {
            SearchText = NaturalLanguageQuery;
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsApplyingNaturalLanguageFilter = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToIssueAsync(string? issueId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issueId))
        {
            return;
        }

        await Shell.Current.GoToAsync($"{AppConstants.RouteIssueDetail}?IssueId={Uri.EscapeDataString(issueId)}");
    }

    private async Task LoadIssuesInternalAsync(bool forceRefresh, CancellationToken ct)
    {
        if (IsLoading)
        {
            return;
        }

        if (!forceRefresh && Issues.Count > 0 && DateTime.UtcNow - _lastLoaded < TimeSpan.FromMinutes(MobileSettings.DataCacheMinutes))
        {
            return;
        }

        try
        {
            IsLoading = true;
            _currentPage = 1;
            _hasMore = true;

            using (_performance.StartTrace("LoadIssues"))
            {
                var filter = ActiveFilter == AppConstants.FilterAll ? null : ActiveFilter;
                var issues = await _api.GetIssuesAsync(filter, SearchText, _currentPage, MobileSettings.PaginationPageSize, ct);
                ct.ThrowIfCancellationRequested();

                Issues.Clear();
                foreach (var issue in issues)
                {
                    Issues.Add(issue);
                }

                _hasMore = issues.Count >= MobileSettings.PaginationPageSize;
                IsEmpty = Issues.Count == 0;
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

    private void DebounceSearch()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(MobileSettings.SearchDebounceMs, cts.Token);
                await MainThread.InvokeOnMainThreadAsync(async () => await LoadIssuesInternalAsync(true, cts.Token));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await _analytics.TrackError(ex);
            }
        }, cts.Token);
    }

    private static string MapStatusToFilter(int? status)
    {
        return status switch
        {
            AppConstants.StatusNewValue => AppConstants.FilterNew,
            AppConstants.StatusInProgressValue => AppConstants.FilterInProgress,
            AppConstants.StatusFixedValue => AppConstants.FilterResolved,
            _ => AppConstants.FilterAll
        };
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
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
