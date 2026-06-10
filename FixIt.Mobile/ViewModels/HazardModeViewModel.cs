using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class HazardModeViewModel : ObservableObject, IDisposable
{
    private const double DefaultLatitude = 42.6977;   // Sofia city centre
    private const double DefaultLongitude = 23.3219;
    private const double NearbyRadiusKm = 10;

    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;
    private bool _subscribed;
    private bool _locationInitialized;
    private double _userLatitude = DefaultLatitude;
    private double _userLongitude = DefaultLongitude;

    public HazardModeViewModel(IApiService api, IAuthService auth, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _auth = auth;
        _analytics = analytics;
        _performance = performance;
        IsLoggedIn = _auth.IsLoggedIn;
        CityId = AppConstants.DefaultCityId;

        // Hazard categories mirror the server HazardType enum. Labels are kept in
        // line with the report screen's category tiles (hardcoded, not localized).
        HazardTypes =
        [
            new() { Key = "Accident", Label = "Accident" },
            new() { Key = "Construction", Label = "Construction" },
            new() { Key = "Pothole", Label = "Pothole" },
            new() { Key = "Flooding", Label = "Flooding" },
            new() { Key = "DamagedInfrastructure", Label = "Infrastructure" },
            new() { Key = "StreetLight", Label = "Street light" },
            new() { Key = "Debris", Label = "Debris" },
            new() { Key = "TrafficCongestion", Label = "Traffic" },
            new() { Key = "Crime", Label = "Crime" },
            new() { Key = "Other", Label = "Other" },
        ];

        SubscribeAuth();
    }

    public ObservableCollection<SafetyHazard> Hazards { get; } = [];

    public ObservableCollection<HazardTypeOption> HazardTypes { get; }

    [ObservableProperty]
    private HtmlWebViewSource? _mapSource;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLocating;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    // ---- Report form (parity with the web "report a hazard" flow) ----

    [ObservableProperty]
    private bool _isReportPanelVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitReport))]
    [NotifyCanExecuteChangedFor(nameof(SubmitReportCommand))]
    private string _reportTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitReport))]
    [NotifyCanExecuteChangedFor(nameof(SubmitReportCommand))]
    private string _reportDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReportAddress))]
    private string _reportAddress = string.Empty;

    [ObservableProperty]
    private string _cityId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitReport))]
    [NotifyCanExecuteChangedFor(nameof(SubmitReportCommand))]
    private string _selectedTypeKey = string.Empty;

    [ObservableProperty]
    private string _selectedSeverity = "Medium";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitReport))]
    [NotifyCanExecuteChangedFor(nameof(SubmitReportCommand))]
    private double? _reportLatitude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitReport))]
    [NotifyCanExecuteChangedFor(nameof(SubmitReportCommand))]
    private double? _reportLongitude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitReport))]
    [NotifyPropertyChangedFor(nameof(HasReportError))]
    [NotifyCanExecuteChangedFor(nameof(SubmitReportCommand))]
    private bool _isSubmittingReport;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReportError))]
    private string _reportError = string.Empty;

    [ObservableProperty]
    private bool _reportSucceeded;

    public bool IsSeverityLow => SelectedSeverity == "Low";
    public bool IsSeverityMedium => SelectedSeverity == "Medium";
    public bool IsSeverityHigh => SelectedSeverity == "High";
    public bool IsSeverityCritical => SelectedSeverity == "Critical";

    public bool HasReportAddress => !string.IsNullOrWhiteSpace(ReportAddress);
    public bool HasReportError => !string.IsNullOrWhiteSpace(ReportError);

    public bool CanSubmitReport =>
        !string.IsNullOrWhiteSpace(ReportTitle)
        && !string.IsNullOrWhiteSpace(ReportDescription)
        && !string.IsNullOrWhiteSpace(SelectedTypeKey)
        && ReportLatitude.HasValue
        && ReportLongitude.HasValue
        && !IsSubmittingReport;

    public async Task OnAppearingAsync()
    {
        SubscribeAuth();
        await _analytics.TrackScreen("HazardMode");

        if (!_locationInitialized)
        {
            await InitializeLocationAsync(_cts.Token);
        }

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

    /// <summary>
    /// Invoked by the page when the user drops/drags the report pin on the map.
    /// Records the coordinates, opens the report panel, and reverse-geocodes for
    /// a human-readable address and the owning city (mirrors the report-issue map).
    /// </summary>
    public async Task OnMapPointSelectedAsync(double latitude, double longitude, CancellationToken ct)
    {
        ReportLatitude = latitude;
        ReportLongitude = longitude;

        if (!IsLoggedIn)
        {
            ErrorMessage = LocalizationService.Get("HazardMode_Report_LoginRequired");
            return;
        }

        ErrorMessage = string.Empty;
        ReportError = string.Empty;
        IsReportPanelVisible = true;
        await TryReverseGeocodeAsync(latitude, longitude, ct);
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnReportAddressChanged(string value) => OnPropertyChanged(nameof(HasReportAddress));

    partial void OnReportErrorChanged(string value) => OnPropertyChanged(nameof(HasReportError));

    partial void OnSelectedSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsSeverityLow));
        OnPropertyChanged(nameof(IsSeverityMedium));
        OnPropertyChanged(nameof(IsSeverityHigh));
        OnPropertyChanged(nameof(IsSeverityCritical));
    }

    [RelayCommand]
    private void SelectType(HazardTypeOption? option)
    {
        if (option is null)
        {
            return;
        }

        HapticService.Click();
        SelectedTypeKey = option.Key;
        foreach (var type in HazardTypes)
        {
            type.IsSelected = type.Key == option.Key;
        }
    }

    [RelayCommand]
    private void SelectSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return;
        }

        HapticService.Click();
        SelectedSeverity = severity.Trim();
    }

    [RelayCommand]
    private void CancelReport()
    {
        ResetReportForm();
    }

    [RelayCommand]
    private async Task LoadHazardsAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            using (_performance.StartTrace("LoadHazardMode"))
            {
                var hazards = await _api.GetNearbyHazardsAsync(_userLatitude, _userLongitude, NearbyRadiusKm, CityId, ct);
                Hazards.Clear();
                foreach (var hazard in hazards)
                {
                    hazard.CanConfirm = IsLoggedIn;
                    Hazards.Add(hazard);
                }

                RebuildMap();
                IsEmpty = Hazards.Count == 0;
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "hazard_mode_cancelled" });
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

    [RelayCommand(CanExecute = nameof(CanSubmitReport))]
    private async Task SubmitReportAsync(CancellationToken ct)
    {
        HapticService.Click();

        if (!IsLoggedIn)
        {
            await Shell.Current.GoToAsync(AppConstants.RouteAccountTabAbsolute);
            return;
        }

        if (!ReportLatitude.HasValue || !ReportLongitude.HasValue)
        {
            ReportError = LocalizationService.Get("HazardMode_Report_LocationRequired");
            return;
        }

        try
        {
            IsSubmittingReport = true;
            ReportError = string.Empty;

            using (_performance.StartTrace("ReportHazard"))
            {
                var result = await _api.ReportHazardAsync(
                    SelectedTypeKey,
                    SelectedSeverity,
                    ReportTitle.Trim(),
                    ReportDescription.Trim(),
                    ReportLatitude.Value,
                    ReportLongitude.Value,
                    string.IsNullOrWhiteSpace(ReportAddress) ? null : ReportAddress.Trim(),
                    string.IsNullOrWhiteSpace(CityId) ? AppConstants.DefaultCityId : CityId,
                    ct);

                ct.ThrowIfCancellationRequested();

                if (result.Success)
                {
                    HapticService.LongPress();
                    await _analytics.TrackEvent("hazard_reported");
                    ReportSucceeded = true;
                    ResetReportForm();
                    await LoadHazardsAsync(ct);
                }
                else
                {
                    ReportError = result.Error ?? LocalizationService.Get("HazardMode_Report_Error");
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "hazard_report_cancelled" });
        }
        catch (Exception ex)
        {
            ReportError = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsSubmittingReport = false;
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

                RebuildMap();
            }
            else
            {
                ErrorMessage = result.Error ?? LocalizationService.Get("Common_Error_Generic");
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "hazard_mode_confirm_cancelled" });
        }
        catch (Exception ex)
        {
            ErrorMessage = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
    }

    private async Task InitializeLocationAsync(CancellationToken ct)
    {
        _locationInitialized = true;

        try
        {
            IsLocating = true;
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            Location? location = null;
            if (status == PermissionStatus.Granted)
            {
                try
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                    location = await Geolocation.Default.GetLocationAsync(request, ct);
                }
                catch (FeatureNotSupportedException)
                {
                    // Simulator without GPS — keep the default centre.
                }
                catch (FeatureNotEnabledException)
                {
                    // Location services disabled — keep the default centre.
                }
                catch (PermissionException)
                {
                }
            }

            if (location != null)
            {
                _userLatitude = location.Latitude;
                _userLongitude = location.Longitude;
            }
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "hazard_location_init" });
        }
        finally
        {
            IsLocating = false;
            RebuildMap();
        }
    }

    private async Task TryReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct)
    {
        try
        {
            var result = await _api.ReverseGeocodeAsync(latitude, longitude, ct);
            if (result is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.Address))
            {
                ReportAddress = result.Address;
            }
            if (!string.IsNullOrWhiteSpace(result.CityId))
            {
                CityId = result.CityId!;
            }
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "hazard_reverse_geocode" });
        }
    }

    private void RebuildMap()
    {
        MapSource = MapHtmlBuilder.BuildHazardMap(
            Hazards,
            _userLatitude,
            _userLongitude,
            zoom: 14,
            reportPinLabel: LocalizationService.Get("HazardMode_Report_DragPin"));
    }

    private void ResetReportForm()
    {
        IsReportPanelVisible = false;
        ReportTitle = string.Empty;
        ReportDescription = string.Empty;
        ReportAddress = string.Empty;
        ReportError = string.Empty;
        SelectedTypeKey = string.Empty;
        SelectedSeverity = "Medium";
        ReportLatitude = null;
        ReportLongitude = null;
        foreach (var type in HazardTypes)
        {
            type.IsSelected = false;
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

    // ---- Test seams ----

    public void SetUserLocationForTesting(double latitude, double longitude)
    {
        _userLatitude = latitude;
        _userLongitude = longitude;
        _locationInitialized = true;
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
