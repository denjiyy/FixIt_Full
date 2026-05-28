using FixIt.Mobile.Constants;

namespace FixIt.Mobile.Views;

public partial class ConnectedAccountsPage : ContentPage
{
    public ConnectedAccountsPage()
    {
        InitializeComponent();
    }

    private async void OnLinkProviderClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string provider)
        {
            var url = $"{AppConstants.BaseUrl}Identity/Account/ExternalLogin?provider={provider}&returnUrl=/settings/connected-accounts";
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }
    }
}
