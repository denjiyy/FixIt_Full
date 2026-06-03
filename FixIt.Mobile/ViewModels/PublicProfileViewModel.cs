using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

[QueryProperty(nameof(UserId), nameof(UserId))]
public partial class PublicProfileViewModel : ObservableObject, IQueryAttributable, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public PublicProfileViewModel(IApiService api, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _analytics = analytics;
        _performance = performance;
    }

    [ObservableProperty]
    private string _userId = string.Empty;

    [ObservableProperty]
    private PublicUserProfile? _profile;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    public ObservableCollection<Issue> Issues { get; } = [];

    public string DisplayName => Profile?.DisplayName ?? string.Empty;
    public string Initials => Profile?.Initials ?? "F";

    // Resolved / reported %, shown in the profile stat strip (mirrors ProfileViewModel.FixRate).
    public int FixRate => Profile is { IssuesReported: > 0 } p
        ? (int)Math.Round((double)p.IssuesResolved / p.IssuesReported * 100)
        : 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(UserId), out var value))
        {
            UserId = Uri.UnescapeDataString(value?.ToString() ?? string.Empty);
        }
    }

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("PublicProfile");
        await LoadProfileAsync(_cts.Token);
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    partial void OnProfileChanged(PublicUserProfile? value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(FixRate));
    }

    private async Task LoadProfileAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            IsEmpty = true;
            return;
        }

        try
        {
            IsLoading = true;
            using (_performance.StartTrace("LoadPublicProfile"))
            {
                Profile = await _api.GetPublicProfileAsync(UserId, ct);
                Issues.Clear();
                IsEmpty = Profile == null;
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "public_profile_cancelled" });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
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
        _cts.Cancel();
        _cts.Dispose();
    }
}
