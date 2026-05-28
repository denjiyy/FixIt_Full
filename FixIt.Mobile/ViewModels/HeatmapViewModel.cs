using CommunityToolkit.Mvvm.ComponentModel;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

[QueryProperty(nameof(CityId), nameof(CityId))]
[QueryProperty(nameof(CityName), nameof(CityName))]
public partial class HeatmapViewModel : ObservableObject, IQueryAttributable
{
    private readonly IApiService _api;
    private readonly IAnalyticsService _analytics;

    public HeatmapViewModel(IApiService api, IAnalyticsService analytics)
    {
        _api = api;
        _analytics = analytics;
    }

    [ObservableProperty] private string _cityId = string.Empty;
    [ObservableProperty] private string _cityName = string.Empty;
    [ObservableProperty] private HtmlWebViewSource? _mapSource;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _openIssues;
    [ObservableProperty] private int _resolvedIssues;
    [ObservableProperty] private int _totalIssues;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(CityId), out var cid))
            CityId = Uri.UnescapeDataString(cid?.ToString() ?? string.Empty);
        if (query.TryGetValue(nameof(CityName), out var cn))
            CityName = Uri.UnescapeDataString(cn?.ToString() ?? string.Empty);
    }

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("Heatmap");
        if (string.IsNullOrWhiteSpace(CityId)) return;
        try
        {
            IsLoading = true;
            var issues = await _api.GetIssuesAsync(page: 1, pageSize: 200);
            var pins = issues
                .Where(i => i.HasCoordinates)
                .Select(i => new SafetyHazard
                {
                    Id = i.Id,
                    Title = i.Title,
                    Address = i.Address,
                    Latitude = i.Latitude,
                    Longitude = i.Longitude,
                    Severity = i.Status
                })
                .ToList();

            MapSource = MapHtmlBuilder.BuildHazardMap(pins);
            TotalIssues = issues.Count;
            ResolvedIssues = issues.Count(i => i.Status == Constants.AppConstants.StatusResolved);
            OpenIssues = TotalIssues - ResolvedIssues;
        }
        catch (Exception ex) { await _analytics.TrackError(ex); }
        finally { IsLoading = false; }
    }
}
