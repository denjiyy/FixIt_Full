using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

[QueryProperty(nameof(TagName), nameof(TagName))]
public partial class TagDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IApiService _api;
    private readonly IAnalyticsService _analytics;
    private int _page = 1;

    public TagDetailViewModel(IApiService api, IAnalyticsService analytics)
    {
        _api = api;
        _analytics = analytics;
    }

    [ObservableProperty] private string _tagName = string.Empty;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<Issue> Issues { get; } = [];
    public bool IsEmpty => Issues.Count == 0 && !IsLoading;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(TagName), out var v))
            TagName = Uri.UnescapeDataString(v?.ToString() ?? string.Empty);
    }

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("TagDetail");
        if (Issues.Count > 0 || string.IsNullOrWhiteSpace(TagName)) return;
        await LoadAsync(default);
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            _page = 1;
            var issues = await _api.GetIssuesByTagAsync(TagName, _page, 20, ct);
            Issues.Clear();
            foreach (var i in issues) Issues.Add(i);
        }
        catch (Exception ex) { await _analytics.TrackError(ex); }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    [RelayCommand]
    private async Task LoadMoreAsync(CancellationToken ct)
    {
        if (IsLoading) return;
        try
        {
            _page++;
            var issues = await _api.GetIssuesByTagAsync(TagName, _page, 20, ct);
            foreach (var i in issues) Issues.Add(i);
        }
        catch (Exception ex) { await _analytics.TrackError(ex); }
    }

    [RelayCommand]
    private async Task NavigateToIssueAsync(Issue? issue, CancellationToken ct)
    {
        if (issue == null) return;
        HapticService.Click();
        await Shell.Current.GoToAsync($"{AppConstants.RouteIssueDetail}?IssueId={Uri.EscapeDataString(issue.Id)}");
    }
}
