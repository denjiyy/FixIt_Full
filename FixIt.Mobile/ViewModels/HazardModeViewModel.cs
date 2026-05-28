using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class HazardModeViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly IAnalyticsService _analytics;

    public HazardModeViewModel(IApiService api, IAnalyticsService analytics)
    {
        _api = api;
        _analytics = analytics;
    }

    [ObservableProperty] private HtmlWebViewSource? _mapSource;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isReporting;
    [ObservableProperty] private bool _showReportForm;

    [ObservableProperty] private string _hazardType = string.Empty;
    [ObservableProperty] private string _hazardSeverity = string.Empty;
    [ObservableProperty] private string _hazardTitle = string.Empty;
    [ObservableProperty] private string _hazardDescription = string.Empty;
    [ObservableProperty] private double _selectedLatitude;
    [ObservableProperty] private double _selectedLongitude;

    public ObservableCollection<SafetyHazard> Hazards { get; } = [];

    public string[] HazardTypes { get; } = ["Crime", "Accident", "Construction", "Pothole", "Flooding", "Other"];
    public string[] SeverityLevels { get; } = ["Low", "Medium", "High", "Critical"];

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("HazardMode");
        await LoadHazardsAsync();
    }

    private async Task LoadHazardsAsync()
    {
        try
        {
            IsLoading = true;
            var hazards = await _api.GetCriticalHazardsAsync();
            Hazards.Clear();
            foreach (var h in hazards) Hazards.Add(h);
            MapSource = MapHtmlBuilder.BuildHazardMap(hazards);
        }
        catch (Exception ex) { await _analytics.TrackError(ex); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleReportForm()
    {
        ShowReportForm = !ShowReportForm;
        if (ShowReportForm)
        {
            HazardType = string.Empty;
            HazardSeverity = string.Empty;
            HazardTitle = string.Empty;
            HazardDescription = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SubmitHazardAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(HazardType) || string.IsNullOrWhiteSpace(HazardSeverity)
            || string.IsNullOrWhiteSpace(HazardTitle) || string.IsNullOrWhiteSpace(HazardDescription))
            return;

        try
        {
            IsReporting = true;
            HapticService.Click();
            var result = await _api.ReportHazardAsync(
                HazardType, HazardSeverity, HazardTitle.Trim(), HazardDescription.Trim(),
                SelectedLatitude, SelectedLongitude, ct);

            if (result.Success)
            {
                ShowReportForm = false;
                await LoadHazardsAsync();
            }
        }
        catch (Exception ex) { await _analytics.TrackError(ex); }
        finally { IsReporting = false; }
    }

    [RelayCommand]
    private async Task ConfirmHazardAsync(SafetyHazard? hazard, CancellationToken ct)
    {
        if (hazard == null) return;
        HapticService.Click();
        var result = await _api.ConfirmHazardAsync(hazard.Id, ct);
        if (result.Success)
        {
            hazard.Confirmations++;
            hazard.CanConfirm = false;
        }
    }
}
