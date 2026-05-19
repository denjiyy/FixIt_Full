using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class LeaderboardViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public LeaderboardViewModel(IApiService api, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _analytics = analytics;
        _performance = performance;
    }

    public ObservableCollection<LeaderboardEntry> Entries { get; } = [];

    [ObservableProperty]
    private string _selectedPeriod = "weekly";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    public bool IsWeekly => SelectedPeriod == "weekly";
    public bool IsMonthly => SelectedPeriod == "monthly";
    public bool IsAllTime => SelectedPeriod == "alltime";

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("Leaderboard");
        if (Entries.Count == 0)
        {
            await LoadLeaderboardAsync(_cts.Token);
        }
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        OnPropertyChanged(nameof(IsWeekly));
        OnPropertyChanged(nameof(IsMonthly));
        OnPropertyChanged(nameof(IsAllTime));
    }

    [RelayCommand]
    private async Task SelectPeriodAsync(string? period, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(period) || SelectedPeriod == period)
        {
            return;
        }

        HapticService.Click();
        SelectedPeriod = period;
        await LoadLeaderboardAsync(ct);
    }

    [RelayCommand]
    private async Task LoadLeaderboardAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            using (_performance.StartTrace("LoadLeaderboard"))
            {
                var result = await _api.GetLeaderboardAsync(SelectedPeriod, ct);
                Entries.Clear();
                foreach (var entry in result.Entries)
                {
                    Entries.Add(entry);
                }

                IsEmpty = Entries.Count == 0;
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "leaderboard_cancelled" });
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
