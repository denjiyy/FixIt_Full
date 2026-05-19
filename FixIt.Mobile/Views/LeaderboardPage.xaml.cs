using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class LeaderboardPage : ContentPage
{
    public LeaderboardPage(LeaderboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LeaderboardViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        LeaderboardRoot.Opacity = 0;
        LeaderboardRoot.TranslationY = 24;
        await Task.WhenAll(
            LeaderboardRoot.FadeTo(1, 350, Easing.CubicOut),
            LeaderboardRoot.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is LeaderboardViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }
}
