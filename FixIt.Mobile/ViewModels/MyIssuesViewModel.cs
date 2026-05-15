using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class MyIssuesViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private int _currentPage = 1;
    private bool _hasMore = true;
    private bool _disposed;

    public MyIssuesViewModel(IApiService api, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _analytics = analytics;
        _performance = performance;
    }

    public ObservableCollection<Issue> Issues { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private bool _isEmpty;

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("MyIssues");
        if (Issues.Count == 0)
        {
            await LoadIssuesInternalAsync(true, _cts.Token);
        }
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    [RelayCommand]
    private async Task LoadIssuesAsync(CancellationToken ct)
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
            var nextPage = _currentPage + 1;
            using (_performance.StartTrace("LoadMyIssuesMore"))
            {
                var issues = await _api.GetMyIssuesAsync(nextPage, MobileSettings.PaginationPageSize, ct);
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
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "my_issues_cancelled" });
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
    private async Task NavigateToIssueAsync(string? issueId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issueId))
        {
            return;
        }

        await Shell.Current.GoToAsync($"{AppConstants.RouteIssueDetail}?IssueId={Uri.EscapeDataString(issueId)}");
    }

    [RelayCommand]
    private async Task DeleteIssueAsync(string? issueId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issueId))
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert(
            LocalizationService.Get("MyIssues_DeleteConfirmTitle"),
            LocalizationService.Get("MyIssues_DeleteConfirmMessage"),
            LocalizationService.Get("Common_Delete"),
            LocalizationService.Get("Common_Cancel"));

        if (!confirmed)
        {
            return;
        }

        try
        {
            using (_performance.StartTrace("DeleteIssue"))
            {
                var result = await _api.DeleteIssueAsync(issueId, ct);
                if (result.Success)
                {
                    var item = Issues.FirstOrDefault(i => i.Id == issueId);
                    if (item != null)
                    {
                        Issues.Remove(item);
                    }

                    IsEmpty = Issues.Count == 0;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "delete_cancelled" });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
    }

    private async Task LoadIssuesInternalAsync(bool forceRefresh, CancellationToken ct)
    {
        if (IsLoading && !forceRefresh)
        {
            return;
        }

        try
        {
            IsLoading = true;
            _currentPage = 1;
            _hasMore = true;
            using (_performance.StartTrace("LoadMyIssues"))
            {
                var issues = await _api.GetMyIssuesAsync(_currentPage, MobileSettings.PaginationPageSize, ct);
                Issues.Clear();
                foreach (var issue in issues)
                {
                    Issues.Add(issue);
                }

                _hasMore = issues.Count >= MobileSettings.PaginationPageSize;
                IsEmpty = Issues.Count == 0;
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "my_issues_cancelled" });
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
        _cts.Cancel();
        _cts.Dispose();
    }
}
