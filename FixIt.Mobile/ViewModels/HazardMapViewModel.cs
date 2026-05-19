using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class HazardMapViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;
    private bool _subscribed;

    public HazardMapViewModel(IApiService api, IAuthService auth, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _auth = auth;
        _analytics = analytics;
        _performance = performance;
        IsLoggedIn = _auth.IsLoggedIn;
        SubscribeAuth();
    }

    public ObservableCollection<SafetyHazard> Hazards { get; } = [];

    [ObservableProperty]
    private HtmlWebViewSource? _mapSource;

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
        await _analytics.TrackScreen("HazardMap");
        if (Hazards.Count == 0)
        {
            await LoadHazardsAsync(_cts.Token);
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
    private async Task LoadHazardsAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            using (_performance.StartTrace("LoadHazardMap"))
            {
                var hazards = await _api.GetCriticalHazardsAsync(ct);
                Hazards.Clear();
                foreach (var hazard in hazards)
                {
                    hazard.CanConfirm = IsLoggedIn;
                    Hazards.Add(hazard);
                }

                MapSource = MapHtmlBuilder.BuildHazardMap(Hazards);
                IsEmpty = Hazards.Count == 0;
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "hazard_map_cancelled" });
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
            var result = await _api.ConfirmHazardAsync(hazardId, ct);
            if (result.Success)
            {
                var hazard = Hazards.FirstOrDefault(h => h.Id == hazardId);
                if (hazard != null)
                {
                    hazard.Confirmations += 1;
                }

                MapSource = MapHtmlBuilder.BuildHazardMap(Hazards);
            }
            else
            {
                ErrorMessage = result.Error ?? LocalizationService.Get("Common_Error_Generic");
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "hazard_map_confirm_cancelled" });
        }
        catch (Exception ex)
        {
            ErrorMessage = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
    }

    private void OnLoginStateChanged(object? sender, bool isLoggedIn)
    {
        IsLoggedIn = isLoggedIn;
        foreach (var hazard in Hazards)
        {
            hazard.CanConfirm = isLoggedIn;
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
