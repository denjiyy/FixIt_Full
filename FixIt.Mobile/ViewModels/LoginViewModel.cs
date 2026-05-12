using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Services;

namespace FixIt.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    public LoginViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isAuthenticated;

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsAuthenticated = await _apiService.LoginAsync(Email, Password);
    }
}
