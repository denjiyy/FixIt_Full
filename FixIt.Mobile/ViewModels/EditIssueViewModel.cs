using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

[QueryProperty(nameof(IssueId), nameof(IssueId))]
public partial class EditIssueViewModel : ObservableObject, IQueryAttributable
{
    private readonly IApiService _api;
    private readonly IAnalyticsService _analytics;

    public EditIssueViewModel(IApiService api, IAnalyticsService analytics)
    {
        _api = api;
        _analytics = analytics;
    }

    [ObservableProperty] private string _issueId = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(IssueId), out var value))
            IssueId = Uri.UnescapeDataString(value?.ToString() ?? string.Empty);
    }

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("EditIssue");
        if (string.IsNullOrWhiteSpace(IssueId)) return;
        try
        {
            IsLoading = true;
            var issue = await _api.GetIssueAsync(IssueId);
            if (issue != null)
            {
                Title = issue.Title;
                Description = issue.Description;
                Address = issue.Address;
            }
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
    private async Task SaveAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Title)) return;
        try
        {
            IsSaving = true;
            HapticService.Click();
            var result = await _api.UpdateIssueAsync(IssueId, Title.Trim(), Description.Trim(), Address.Trim(), ct);
            if (result.Success)
            {
                await Shell.Current.DisplayAlert(
                    LocalizationService.Get("Edit_Title"),
                    LocalizationService.Get("Edit_Success"),
                    LocalizationService.Get("Common_OK"));
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
