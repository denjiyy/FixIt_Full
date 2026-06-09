using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class HazardModePage : ContentPage
{
    public HazardModePage(HazardModeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
}
