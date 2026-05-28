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
    private readonly IThemePreferenceService _theme;

    private bool _isBusy;
    private bool _anonymousReporting;
    private bool _crimeAlerts = true;
    private bool _accidentAlerts = true;
    private bool _infrastructureAlerts = true;
    private double _radiusKm = 5;
    private string _severityThreshold = "Medium";
    private string _profileVisibility = "Public";
    private AppThemePreference _themePreference;

    private bool _emailEnabled = true;
    private bool _weeklyReports = true;
    private bool _emailSafetyAlerts = true;
    private bool _emailReminders = true;
    private string _selectedCityName = string.Empty;
    private List<CityInfo> _cities = [];
    private List<string> _cityNames = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsViewModel(IApiService api, IAuthService auth, IThemePreferenceService theme)
    {
        _api = api;
        _auth = auth;
        _theme = theme;
        _themePreference = theme.GetPreference();

        LoadCommand = new Command(async () => await LoadAsync());
        ToggleAnonymousCommand = new Command<bool>(async value => await ToggleAnonymousAsync(value));
        SaveAlertsCommand = new Command(async () => await SaveAlertsAsync(), () => !IsBusy);
        SaveVisibilityCommand = new Command(async () => await SaveVisibilityAsync(), () => !IsBusy);
        SaveCityPreferenceCommand = new Command(async () => await SaveCityPreferenceAsync(), () => !IsBusy);
        SaveEmailPreferencesCommand = new Command(async () => await SaveEmailPreferencesAsync(), () => !IsBusy);
        SelectThemeCommand = new Command<string>(SelectTheme);

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
    public ICommand SaveCityPreferenceCommand { get; }
    public ICommand SaveEmailPreferencesCommand { get; }
    public ICommand SelectThemeCommand { get; }

    public bool IsThemeSystem => _themePreference == AppThemePreference.System;
    public bool IsThemeLight => _themePreference == AppThemePreference.Light;
    public bool IsThemeDark => _themePreference == AppThemePreference.Dark;

    private void SelectTheme(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var target = key.ToLowerInvariant() switch
        {
            "light" => AppThemePreference.Light,
            "dark" => AppThemePreference.Dark,
            _ => AppThemePreference.System,
        };

        if (_themePreference == target)
        {
            return;
        }

        _themePreference = target;
        _theme.SetPreference(target);
        OnPropertyChanged(nameof(IsThemeSystem));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));
    }

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

    public bool EmailEnabled
    {
        get => _emailEnabled;
        set { if (_emailEnabled != value) { _emailEnabled = value; OnPropertyChanged(); } }
    }

    public bool WeeklyReports
    {
        get => _weeklyReports;
        set { if (_weeklyReports != value) { _weeklyReports = value; OnPropertyChanged(); } }
    }

    public bool EmailSafetyAlerts
    {
        get => _emailSafetyAlerts;
        set { if (_emailSafetyAlerts != value) { _emailSafetyAlerts = value; OnPropertyChanged(); } }
    }

    public bool EmailReminders
    {
        get => _emailReminders;
        set { if (_emailReminders != value) { _emailReminders = value; OnPropertyChanged(); } }
    }

    public List<string> CityNames
    {
        get => _cityNames;
        set { _cityNames = value; OnPropertyChanged(); }
    }

    public string SelectedCityName
    {
        get => _selectedCityName;
        set { if (_selectedCityName != value) { _selectedCityName = value; OnPropertyChanged(); } }
    }

    public async Task LoadCitiesAndEmailAsync()
    {
        try
        {
            _cities = await _api.GetCitiesAsync();
            CityNames = _cities.Select(c => c.Name).ToList();

            var cityPref = await _api.GetCityPreferenceAsync();
            if (!string.IsNullOrWhiteSpace(cityPref))
            {
                var match = _cities.FirstOrDefault(c => c.Id == cityPref);
                if (match != null) SelectedCityName = match.Name;
            }

            var emailPrefs = await _api.GetEmailPreferencesAsync();
            if (emailPrefs != null)
            {
                EmailEnabled = emailPrefs.Enabled;
                WeeklyReports = emailPrefs.WeeklyReports;
                EmailSafetyAlerts = emailPrefs.SafetyAlerts;
                EmailReminders = emailPrefs.Reminders;
            }
        }
        catch { /* non-critical */ }
    }

    private async Task SaveCityPreferenceAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(SelectedCityName)) return;
        IsBusy = true;
        try
        {
            var city = _cities.FirstOrDefault(c => c.Name == SelectedCityName);
            if (city != null)
            {
                var result = await _api.SaveCityPreferenceAsync(city.Id);
                if (result.Success)
                    await ApiErrorHandler.ShowAsync(LocalizationService.Get("Settings_Alerts_Saved"));
            }
        }
        finally { IsBusy = false; }
    }

    private async Task SaveEmailPreferencesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var prefs = new EmailPreferences
            {
                Enabled = EmailEnabled,
                WeeklyReports = WeeklyReports,
                SafetyAlerts = EmailSafetyAlerts,
                Reminders = EmailReminders
            };
            var result = await _api.SaveEmailPreferencesAsync(prefs);
            if (result.Success)
                await ApiErrorHandler.ShowAsync(LocalizationService.Get("Settings_Alerts_Saved"));
        }
        finally { IsBusy = false; }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
