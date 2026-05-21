using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class HazardMapPage : ContentPage
{
    public HazardMapPage(HazardMapViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        HazardMapRoot.Opacity = 0;
        HazardMapRoot.TranslationY = 24;
        var animationTask = Task.WhenAll(
            HazardMapRoot.FadeTo(1, 350, Easing.CubicOut),
            HazardMapRoot.TranslateTo(0, 0, 350, Easing.CubicOut));

        if (BindingContext is HazardMapViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        await animationTask;
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is HazardMapViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (BindingContext is HazardMapViewModel viewModel && sender is Button { CommandParameter: string hazardId })
        {
            await viewModel.ConfirmHazardCommand.ExecuteAsync(hazardId);
        }
    }
}
