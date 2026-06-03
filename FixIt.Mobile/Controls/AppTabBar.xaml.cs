using FixIt.Mobile.Constants;
using FixIt.Mobile.Services;

namespace FixIt.Mobile.Controls;

// Custom bottom navigation matching the FixIt Mobile Feed design: four flat tabs
// (Home · Explore · Alerts · You) around a centre elevated report FAB. Shell's native
// tab bar is hidden (Shell.TabBarIsVisible=False); each tab-root page hosts one of these.
// The Active property ("home"/"explore"/"alerts"/"you") drives the selected styling via
// XAML triggers; taps switch Shell tab roots, the FAB pushes the report flow.
public partial class AppTabBar : ContentView
{
    public static readonly BindableProperty ActiveProperty =
        BindableProperty.Create(nameof(Active), typeof(string), typeof(AppTabBar), string.Empty);

    public string Active
    {
        get => (string)GetValue(ActiveProperty);
        set => SetValue(ActiveProperty, value);
    }

    public AppTabBar()
    {
        InitializeComponent();
    }

    private async void OnHomeTapped(object? sender, TappedEventArgs e) => await NavigateAsync(AppConstants.RouteHome, "home");

    private async void OnExploreTapped(object? sender, TappedEventArgs e) => await NavigateAsync(AppConstants.RouteIssues, "explore");

    private async void OnAlertsTapped(object? sender, TappedEventArgs e) => await NavigateAsync(AppConstants.RouteAlerts, "alerts");

    private async void OnYouTapped(object? sender, TappedEventArgs e) => await NavigateAsync(AppConstants.RouteAccountTabAbsolute, "you");

    private async void OnReportTapped(object? sender, TappedEventArgs e)
    {
        HapticService.Click();
        // Shell.OnNavigating gates this to sign-in when the user is logged out.
        await Shell.Current.GoToAsync(AppConstants.RouteReportIssueTab);
    }

    private async Task NavigateAsync(string route, string tab)
    {
        if (string.Equals(Active, tab, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        HapticService.Click();
        await Shell.Current.GoToAsync(route);
    }
}
