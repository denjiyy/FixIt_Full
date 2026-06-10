using System.Globalization;
using FixIt.Mobile.Models;
using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class HazardModePage : ContentPage
{
    private const string MapBridgeScheme = "fixit://location";

    public HazardModePage(HazardModeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        HazardMapWebView.Navigating += OnMapNavigating;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        HazardModeRoot.Opacity = 0;
        HazardModeRoot.TranslationY = 24;
        var animationTask = Task.WhenAll(
            HazardModeRoot.FadeTo(1, 350, Easing.CubicOut),
            HazardModeRoot.TranslateTo(0, 0, 350, Easing.CubicOut));

        if (BindingContext is HazardModeViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        await animationTask;
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is HazardModeViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (BindingContext is HazardModeViewModel viewModel && sender is Button { CommandParameter: string hazardId })
        {
            await viewModel.ConfirmHazardCommand.ExecuteAsync(hazardId);
        }
    }

    // Hazard-type chips are wired in code-behind for the same reason the report
    // screen's category tiles are: the cross-context command binding from inside
    // a BindableLayout item template doesn't fire reliably.
    private void OnHazardTypeTapped(object? sender, TappedEventArgs e)
    {
        var option = (sender as Element)?.BindingContext as HazardTypeOption ?? e.Parameter as HazardTypeOption;
        if (option is not null && BindingContext is HazardModeViewModel viewModel)
        {
            viewModel.SelectTypeCommand.Execute(option);
        }
    }

    // Map → page bridge: the WebView fires fixit://location?lat=…&lng=… when the
    // user drops or drags the report pin. Mirrors ReportIssuePage.
    private async void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Url) || !e.Url.StartsWith(MapBridgeScheme, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;

        if (BindingContext is not HazardModeViewModel viewModel)
        {
            return;
        }

        if (!TryParseLatLng(e.Url, out var lat, out var lng))
        {
            return;
        }

        try
        {
            await viewModel.OnMapPointSelectedAsync(lat, lng, CancellationToken.None);
        }
        catch
        {
            // Reverse-geocode failures are already logged by the VM; never let the WebView bridge crash the page.
        }
    }

    private static bool TryParseLatLng(string url, out double lat, out double lng)
    {
        lat = 0;
        lng = 0;
        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0) return false;

        var query = url[(queryIndex + 1)..];
        double? parsedLat = null;
        double? parsedLng = null;
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0) continue;
            var key = pair[..idx];
            var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
            if (string.Equals(key, "lat", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var l))
            {
                parsedLat = l;
            }
            else if (string.Equals(key, "lng", StringComparison.OrdinalIgnoreCase) &&
                     double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var g))
            {
                parsedLng = g;
            }
        }

        if (parsedLat is null || parsedLng is null) return false;
        lat = parsedLat.Value;
        lng = parsedLng.Value;
        return true;
    }
}
