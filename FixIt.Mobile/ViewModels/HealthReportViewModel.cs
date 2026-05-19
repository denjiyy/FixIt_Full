using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class HealthReportViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public HealthReportViewModel(IApiService api, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _analytics = analytics;
        _performance = performance;
    }

    [ObservableProperty]
    private CityHealthReport _report = new();

    [ObservableProperty]
    private bool _isLoading;

    public int HealthScoreDisplay => (int)Math.Round(Report.HealthScore);
    public double HealthScoreProgress => Math.Clamp(Report.HealthScore / 100d, 0, 1);
    public int Engagement => Report.TotalUpvotes + Report.TotalComments;

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("HealthReport");
        await LoadReportAsync(_cts.Token);
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    partial void OnReportChanged(CityHealthReport value)
    {
        OnPropertyChanged(nameof(HealthScoreDisplay));
        OnPropertyChanged(nameof(HealthScoreProgress));
        OnPropertyChanged(nameof(Engagement));
    }

    [RelayCommand]
    private async Task LoadReportAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            using (_performance.StartTrace("LoadHealthReport"))
            {
                Report = await _api.GetHealthReportAsync(AppConstants.DefaultCityId, ct);
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "health_report_cancelled" });
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
