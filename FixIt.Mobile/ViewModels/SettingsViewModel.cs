using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

/// <summary>
/// Backs the consolidated Settings screen that covers anonymous reporting,
/// alert preferences, and profile visibility — mirrors the web settings
/// hub (spec §5.1 features 5, 6, 7).
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IApiService _api;
    private readonly IAuthService _auth;

    private bool _isBusy;
    private bool _anonymousReporting;
    private bool _crimeAlerts = true;
    private bool _accidentAlerts = true;
    private bool _infrastructureAlerts = true;
    private double _radiusKm = 5;
    private string _severityThreshold = "Medium";
    private string _profileVisibility = "Public";

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsViewModel(IApiService api, IAuthService auth)
    {
        _api = api;
        _auth = auth;

        LoadCommand = new Command(async () => await LoadAsync());
        ToggleAnonymousCommand = new Command<bool>(async value => await ToggleAnonymousAsync(value));
        SaveAlertsCommand = new Command(async () => await SaveAlertsAsync(), () => !IsBusy);
        SaveVisibilityCommand = new Command(async () => await SaveVisibilityAsync(), () => !IsBusy);

        SeverityOptions = new[] { "Low", "Medium", "High", "Critical" };
        VisibilityOptions = new[]
        {
            LocalizationService.Get("Settings_ProfileVisibility_Public"),
            LocalizationService.Get("Settings_ProfileVisibility_Private")
        };
    }

    public ICommand LoadCommand { get; }
    public ICommand ToggleAnonymousCommand { get; }
    public ICommand SaveAlertsCommand { get; }
    public ICommand SaveVisibilityCommand { get; }

    public string[] SeverityOptions { get; }
    public string[] VisibilityOptions { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); ((Command)SaveAlertsCommand).ChangeCanExecute(); ((Command)SaveVisibilityCommand).ChangeCanExecute(); } }
    }

    public bool AnonymousReporting
    {
        get => _anonymousReporting;
        set { if (_anonymousReporting != value) { _anonymousReporting = value; OnPropertyChanged(); } }
    }

    public bool CrimeAlerts
    {
        get => _crimeAlerts;
        set { if (_crimeAlerts != value) { _crimeAlerts = value; OnPropertyChanged(); } }
    }

    public bool AccidentAlerts
    {
        get => _accidentAlerts;
        set { if (_accidentAlerts != value) { _accidentAlerts = value; OnPropertyChanged(); } }
    }

    public bool InfrastructureAlerts
    {
        get => _infrastructureAlerts;
        set { if (_infrastructureAlerts != value) { _infrastructureAlerts = value; OnPropertyChanged(); } }
    }

    public double RadiusKm
    {
        get => _radiusKm;
        set
        {
            var clamped = Math.Clamp(value, 1, 20);
            if (Math.Abs(_radiusKm - clamped) > 0.01)
            {
                _radiusKm = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RadiusLabel));
            }
        }
    }

    public string RadiusLabel => $"{Math.Round(RadiusKm)} km";

    public string SeverityThreshold
    {
        get => _severityThreshold;
        set { if (_severityThreshold != value) { _severityThreshold = value; OnPropertyChanged(); } }
    }

    public string ProfileVisibility
    {
        get => _profileVisibility;
        set { if (_profileVisibility != value) { _profileVisibility = value; OnPropertyChanged(); } }
    }

    private async Task LoadAsync()
    {
        if (IsBusy) { return; }
        IsBusy = true;
        try
        {
            var prefs = await _api.GetAlertPreferencesAsync();
            if (prefs != null)
            {
                CrimeAlerts = prefs.CrimeAlertsEnabled;
                AccidentAlerts = prefs.AccidentAlertsEnabled;
                InfrastructureAlerts = prefs.InfrastructureAlertsEnabled;
                RadiusKm = Math.Clamp(prefs.RadiusKm, 1, 20);
                SeverityThreshold = prefs.SeverityThreshold;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ToggleAnonymousAsync(bool value)
    {
        // Optimistic UI: assume success, roll back on failure.
        var previous = AnonymousReporting;
        AnonymousReporting = value;

        var result = await _api.ToggleAnonymousReportingAsync(value);
        if (!result.Success)
        {
            AnonymousReporting = previous;
            await ApiErrorHandler.HandleAsync(result, _auth);
        }
    }

    private async Task SaveAlertsAsync()
    {
        if (IsBusy) { return; }
        IsBusy = true;
        try
        {
            var prefs = new AlertPreferences
            {
                CrimeAlertsEnabled = CrimeAlerts,
                AccidentAlertsEnabled = AccidentAlerts,
                InfrastructureAlertsEnabled = InfrastructureAlerts,
                RadiusKm = RadiusKm,
                SeverityThreshold = SeverityThreshold
            };
            var result = await _api.SaveAlertPreferencesAsync(prefs);
            if (result.Success)
            {
                await ApiErrorHandler.ShowAsync(LocalizationService.Get("Settings_Alerts_Saved"));
            }
            else
            {
                await ApiErrorHandler.HandleAsync(result, _auth);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveVisibilityAsync()
    {
        if (IsBusy) { return; }
        IsBusy = true;
        try
        {
            // Map the localized display value back to the canonical "Public"/"Private"
            // payload the API expects regardless of UI language.
            var canonical = ProfileVisibility == LocalizationService.Get("Settings_ProfileVisibility_Private")
                ? "Private"
                : "Public";

            var result = await _api.SetProfileVisibilityAsync(canonical);
            if (result.Success)
            {
                await ApiErrorHandler.ShowAsync(LocalizationService.Get("Settings_ProfileVisibility_Saved"));
            }
            else
            {
                await ApiErrorHandler.HandleAsync(result, _auth);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
