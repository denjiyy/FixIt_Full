using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class PublicProfilePage : ContentPage
{
    public PublicProfilePage(PublicProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is PublicProfileViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        PublicProfileRoot.Opacity = 0;
        PublicProfileRoot.TranslationY = 24;
        await Task.WhenAll(
            PublicProfileRoot.FadeTo(1, 350, Easing.CubicOut),
            PublicProfileRoot.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is PublicProfileViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }
}
