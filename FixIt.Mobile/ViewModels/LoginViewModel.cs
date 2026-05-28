using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject, IDisposable
{
    private static readonly Regex EmailRegex = new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IAnalyticsService _analytics;
    private readonly IAuthService _auth;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public LoginViewModel(IAuthService auth, IAnalyticsService analytics)
    {
        _auth = auth;
        _analytics = analytics;
    }

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _emailError = string.Empty;

    [ObservableProperty]
    private string _passwordError = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public bool HasEmailError => !string.IsNullOrWhiteSpace(EmailError);

    public bool HasPasswordError => !string.IsNullOrWhiteSpace(PasswordError);

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("Login");
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    partial void OnEmailChanged(string value)
    {
        if (HasEmailError)
        {
            EmailError = string.Empty;
            OnPropertyChanged(nameof(HasEmailError));
        }
    }

    partial void OnPasswordChanged(string value)
    {
        if (HasPasswordError)
        {
            PasswordError = string.Empty;
            OnPropertyChanged(nameof(HasPasswordError));
        }
    }

    partial void OnEmailErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasEmailError));
    }

    partial void OnPasswordErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasPasswordError));
    }

    [RelayCommand]
    private async Task LoginAsync(CancellationToken ct)
    {
        if (!Validate())
        {
            HasError = true;
            ErrorMessage = LocalizationService.Get("Login_Error_Empty");
            await _analytics.TrackEvent("login_failed", new Dictionary<string, string> { ["reason"] = "validation" });
            return;
        }

        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            var result = await _auth.LoginAsync(Email, Password, ct);
            ct.ThrowIfCancellationRequested();

            if (result.Success)
            {
                Password = string.Empty;
                HapticService.LongPress();
                await _analytics.TrackEvent("login_success");
                await Shell.Current.GoToAsync(AppConstants.RouteHome);
            }
            else
            {
                HasError = true;
                ErrorMessage = result.Error ?? LocalizationService.Get("Login_Error_Invalid");
                await _analytics.TrackEvent("login_failed", new Dictionary<string, string> { ["reason"] = ErrorMessage });
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "login_cancelled" });
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool Validate()
    {
        EmailError = string.Empty;
        PasswordError = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = LocalizationService.Get("Login_Error_EmailRequired");
        }
        else if (!EmailRegex.IsMatch(Email))
        {
            EmailError = LocalizationService.Get("Login_Error_EmailInvalid");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = LocalizationService.Get("Login_Error_PasswordRequired");
        }

        return string.IsNullOrWhiteSpace(EmailError) && string.IsNullOrWhiteSpace(PasswordError);
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync("register");
    }

    [RelayCommand]
    private async Task GoToForgotPasswordAsync()
    {
        await Shell.Current.GoToAsync(Constants.AppConstants.RouteForgotPassword);
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
