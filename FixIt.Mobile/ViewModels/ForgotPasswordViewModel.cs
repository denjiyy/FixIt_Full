using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class ForgotPasswordViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly IAnalyticsService _analytics;

    public ForgotPasswordViewModel(IApiService api, IAnalyticsService analytics)
    {
        _api = api;
        _analytics = analytics;
    }

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private bool _isSent;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand]
    private async Task SendResetLinkAsync(CancellationToken ct)
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = LocalizationService.Get("Login_Error_EmptyEmail");
            return;
        }

        try
        {
            IsSending = true;
            HapticService.Click();
            await _api.ForgotPasswordAsync(Email.Trim(), ct);
            IsSent = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsSending = false;
        }
    }
}
