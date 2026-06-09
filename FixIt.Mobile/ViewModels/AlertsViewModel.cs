using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class AlertsViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;
    private bool _subscribed;

    public AlertsViewModel(IApiService api, IAuthService auth, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _auth = auth;
        _analytics = analytics;
        _performance = performance;
        IsLoggedIn = _auth.IsLoggedIn;
        SubscribeAuth();
    }

    public ObservableCollection<SafetyHazard> Alerts { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task OnAppearingAsync()
    {
        SubscribeAuth();
        await _analytics.TrackScreen("Alerts");
        if (Alerts.Count == 0)
        {
            await LoadAlertsAsync(_cts.Token);
        }
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
        UnsubscribeAuth();
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    [RelayCommand]
    private async Task LoadAlertsAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            using (_performance.StartTrace("LoadAlerts"))
            {
                var alerts = await _api.GetCriticalHazardsAsync(ct);
                Alerts.Clear();
                foreach (var alert in alerts)
                {
                    alert.CanConfirm = IsLoggedIn;
                    Alerts.Add(alert);
                }

                IsEmpty = Alerts.Count == 0;
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "alerts_cancelled" });
        }
        catch (Exception ex)
        {
            ErrorMessage = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmHazardAsync(string? hazardId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hazardId) || !IsLoggedIn)
        {
            return;
        }

        HapticService.Click();
        try
        {
            using (_performance.StartTrace("ConfirmHazard"))
            {
                var result = await _api.ConfirmHazardAsync(hazardId, ct);
                if (result.Success)
                {
                    var alert = Alerts.FirstOrDefault(a => a.Id == hazardId);
                    if (alert != null)
                    {
                        alert.Confirmations += 1;
                    }
                }
                else
                {
                    ErrorMessage = result.Error ?? LocalizationService.Get("Common_Error_Generic");
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "confirm_cancelled" });
        }
        catch (Exception ex)
        {
            ErrorMessage = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
    }

    [RelayCommand]
    private async Task NavigateToMapAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteHazardMode);
    }

    private void OnLoginStateChanged(object? sender, bool isLoggedIn)
    {
        IsLoggedIn = isLoggedIn;
        foreach (var alert in Alerts)
        {
            alert.CanConfirm = isLoggedIn;
        }
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
        _cts.Cancel();
        _cts.Dispose();
    }
}
