using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class ProfileViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public ProfileViewModel(IApiService api, IAuthService auth, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _auth = auth;
        _analytics = analytics;
        _performance = performance;
        UpdateLanguageState();
        LocalizationService.CultureChanged += OnCultureChanged;
    }

    [ObservableProperty]
    private UserInfo? _currentUser;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEnglish;

    [ObservableProperty]
    private bool _isBulgarian;

    [ObservableProperty]
    private ObservableCollection<Issue> _myIssues = [];

    [ObservableProperty]
    private int _issuesReported;

    [ObservableProperty]
    private int _issuesResolved;

    public string DisplayName => CurrentUser?.DisplayName ?? LocalizationService.Get("Profile_CommunityMember");

    public string Email => CurrentUser?.Email ?? string.Empty;

    public string TrustLevel => CurrentUser?.TrustLevel ?? "Community";

    public int ReputationPoints => CurrentUser?.ReputationPoints ?? 0;

    public string Initials
    {
        get
        {
            var displayName = CurrentUser?.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "F";
            }

            var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 1)
            {
                return parts[0][0].ToString().ToUpperInvariant();
            }

            return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
        }
    }

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("Profile");
        await LoadProfileAsync(_cts.Token);
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    partial void OnCurrentUserChanged(UserInfo? value)
    {
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(TrustLevel));
        OnPropertyChanged(nameof(ReputationPoints));
    }

    [RelayCommand]
    private async Task LoadProfileAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            using (_performance.StartTrace("LoadProfile"))
            {
                CurrentUser = await _api.GetCurrentUserAsync(ct);
                var issues = await _api.GetMyIssuesAsync(1, 5, ct);
                ct.ThrowIfCancellationRequested();

                MyIssues = new ObservableCollection<Issue>(issues.Take(5));
                IssuesReported = issues.Count;
                IssuesResolved = issues.Count(i => i.Status == AppConstants.StatusResolved);
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "profile_cancelled" });
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

    [RelayCommand]
    private async Task ViewAllIssuesAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteMyIssues);
    }

    [RelayCommand]
    private async Task ViewHealthReportAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteHealthReport);
    }

    [RelayCommand]
    private async Task ViewHazardMapAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Shell.Current.GoToAsync(AppConstants.RouteHazardMap);
    }

    [RelayCommand]
    private async Task SwitchLanguageAsync(string? cultureName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return;
        }

        LocalizationService.SetCulture(cultureName);
        HapticService.Click();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LogoutAsync(CancellationToken ct)
    {
        HapticService.Click();
        await _auth.LogoutAsync(ct);
        await Shell.Current.GoToAsync(AppConstants.RouteSignInTabAbsolute);
    }

    private void OnCultureChanged(object? sender, System.Globalization.CultureInfo e)
    {
        UpdateLanguageState();
        OnPropertyChanged(nameof(DisplayName));
    }

    private void UpdateLanguageState()
    {
        IsEnglish = LocalizationService.Instance.CurrentCulture.TwoLetterISOLanguageName == "en";
        IsBulgarian = LocalizationService.Instance.CurrentCulture.TwoLetterISOLanguageName == "bg";
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
        LocalizationService.CultureChanged -= OnCultureChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
