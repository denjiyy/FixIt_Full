using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class CitiesViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly IAnalyticsService _analytics;

    public CitiesViewModel(IApiService api, IAnalyticsService analytics)
    {
        _api = api;
        _analytics = analytics;
    }

    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<CityInfo> Cities { get; } = [];
    public bool IsEmpty => Cities.Count == 0 && !IsLoading;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("Cities");
        if (Cities.Count > 0) return;
        try
        {
            IsLoading = true;
            var cities = await _api.GetCitiesAsync();
            Cities.Clear();
            foreach (var c in cities) Cities.Add(c);
        }
        catch (Exception ex) { await _analytics.TrackError(ex); }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    [RelayCommand]
    private async Task NavigateToCityAsync(CityInfo? city, CancellationToken ct)
    {
        if (city == null) return;
        HapticService.Click();
        await Shell.Current.GoToAsync($"{AppConstants.RouteHeatmap}?CityId={Uri.EscapeDataString(city.Id)}&CityName={Uri.EscapeDataString(city.Name)}");
    }

    [RelayCommand]
    private async Task ViewHealthReportAsync(CityInfo? city, CancellationToken ct)
    {
        if (city == null) return;
        HapticService.Click();
        await Shell.Current.GoToAsync($"{AppConstants.RouteHealthReport}?CityId={Uri.EscapeDataString(city.Id)}");
    }
}
