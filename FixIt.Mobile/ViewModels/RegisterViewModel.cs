using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class RegisterViewModel : ObservableObject, IDisposable
{
    private static readonly Regex EmailRegex = new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const int MinimumPasswordLength = 8;

    private readonly IAnalyticsService _analytics;
    private readonly IAuthService _auth;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public RegisterViewModel(IAuthService auth, IAnalyticsService analytics)
    {
        _auth = auth;
        _analytics = analytics;
    }

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _fullNameError = string.Empty;

    [ObservableProperty]
    private string _emailError = string.Empty;

    [ObservableProperty]
    private string _passwordError = string.Empty;

    [ObservableProperty]
    private string _confirmPasswordError = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public bool HasFullNameError => !string.IsNullOrWhiteSpace(FullNameError);
    public bool HasEmailError => !string.IsNullOrWhiteSpace(EmailError);
    public bool HasPasswordError => !string.IsNullOrWhiteSpace(PasswordError);
    public bool HasConfirmPasswordError => !string.IsNullOrWhiteSpace(ConfirmPasswordError);

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("Register");
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    partial void OnFullNameChanged(string value)
    {
        if (HasFullNameError)
        {
            FullNameError = string.Empty;
            OnPropertyChanged(nameof(HasFullNameError));
        }
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

    partial void OnConfirmPasswordChanged(string value)
    {
        if (HasConfirmPasswordError)
        {
            ConfirmPasswordError = string.Empty;
            OnPropertyChanged(nameof(HasConfirmPasswordError));
        }
    }

    partial void OnFullNameErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasFullNameError));
    }

    partial void OnEmailErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasEmailError));
    }

    partial void OnPasswordErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasPasswordError));
    }

    partial void OnConfirmPasswordErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasConfirmPasswordError));
    }

    [RelayCommand]
    private async Task RegisterAsync(CancellationToken ct)
    {
        if (!Validate())
        {
            HasError = true;
            ErrorMessage = LocalizationService.Get("Register_Error_FormInvalid");
            await _analytics.TrackEvent("register_failed", new Dictionary<string, string> { ["reason"] = "validation" });
            return;
        }

        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            var result = await _auth.RegisterAsync(FullName, Email, Password, ct);
            ct.ThrowIfCancellationRequested();

            if (result.Success)
            {
                ClearFormFields();
                HapticService.LongPress();
                await _analytics.TrackEvent("register_success");
                await Shell.Current.GoToAsync(AppConstants.RouteHome);
            }
            else
            {
                HasError = true;
                ErrorMessage = result.Error ?? LocalizationService.Get("Register_Error_RegistrationFailed");
                await _analytics.TrackEvent("register_failed", new Dictionary<string, string> { ["reason"] = ErrorMessage });
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "register_cancelled" });
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

    [RelayCommand]
    private async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync(AppConstants.RouteAccountTabAbsolute);
    }

    private bool Validate()
    {
        FullNameError = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
        ConfirmPasswordError = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(FullName))
        {
            FullNameError = LocalizationService.Get("Register_Error_FullNameRequired");
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = LocalizationService.Get("Register_Error_EmailRequired");
        }
        else if (!EmailRegex.IsMatch(Email))
        {
            EmailError = LocalizationService.Get("Register_Error_EmailInvalid");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = LocalizationService.Get("Register_Error_PasswordRequired");
        }
        else if (Password.Length < MinimumPasswordLength)
        {
            PasswordError = LocalizationService.Get("Register_Error_PasswordTooShort");
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ConfirmPasswordError = LocalizationService.Get("Register_Error_ConfirmPasswordRequired");
        }
        else if (Password != ConfirmPassword)
        {
            ConfirmPasswordError = LocalizationService.Get("Register_Error_PasswordsMismatch");
        }

        return string.IsNullOrWhiteSpace(FullNameError)
            && string.IsNullOrWhiteSpace(EmailError)
            && string.IsNullOrWhiteSpace(PasswordError)
            && string.IsNullOrWhiteSpace(ConfirmPasswordError);
    }

    private void ClearFormFields()
    {
        FullName = string.Empty;
        Email = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
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
