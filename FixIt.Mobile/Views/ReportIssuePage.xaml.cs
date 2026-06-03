using System.ComponentModel;
using System.Globalization;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class ReportIssuePage : ContentPage
{
    private const string MapBridgeScheme = "fixit://location";

    public ReportIssuePage(ReportIssueViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        MapWebView.Navigating += OnMapNavigating;
    }

    protected override void OnBindingContextChanged()
    {
        if (BindingContext is ReportIssueViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        base.OnBindingContextChanged();

        if (BindingContext is ReportIssueViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ReportIssueViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            await viewModel.OnAppearingAsync();
        }

        FormRoot.TranslationY = 25;
        FormRoot.Opacity = 0;

        await Task.WhenAll(
            FormRoot.FadeTo(1, 350, Easing.CubicOut),
            FormRoot.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is ReportIssueViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppConstants.RouteHome);
    }

    // Category selection is wired in code-behind: the cross-context x:Reference command
    // binding from inside the BindableLayout item template didn't fire reliably. The
    // tapped CategoryOption comes from the gesture's BindingContext (CommandParameter fallback).
    private void OnCategoryTapped(object? sender, TappedEventArgs e)
    {
        var option = (sender as Element)?.BindingContext as CategoryOption ?? e.Parameter as CategoryOption;
        if (option is not null && BindingContext is ReportIssueViewModel viewModel)
        {
            viewModel.SelectCategoryCommand.Execute(option);
        }
    }

    private async void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Url) || !e.Url.StartsWith(MapBridgeScheme, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;

        if (BindingContext is not ReportIssueViewModel viewModel)
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

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportIssueViewModel.SubmissionSucceeded) &&
            BindingContext is ReportIssueViewModel { SubmissionSucceeded: true })
        {
            SuccessCheckLabel.IsVisible = true;
            await Task.WhenAll(
                SuccessCheckLabel.FadeTo(1, 120, Easing.CubicOut),
                SuccessCheckLabel.ScaleTo(1.1, 220, Easing.SpringOut));
            await SuccessCheckLabel.ScaleTo(1.0, 120, Easing.CubicOut);
        }
    }
}
