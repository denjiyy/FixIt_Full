using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class AlertsPage : ContentPage
{
    public AlertsPage(AlertsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        AlertsCollection.Opacity = 0;
        AlertsCollection.TranslationY = 24;

        if (BindingContext is AlertsViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        await Task.WhenAll(
            AlertsCollection.FadeTo(1, 350, Easing.CubicOut),
            AlertsCollection.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is AlertsViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (BindingContext is AlertsViewModel viewModel && sender is Button { CommandParameter: string hazardId })
        {
            await viewModel.ConfirmHazardCommand.ExecuteAsync(hazardId);
        }
    }
}
