using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ProfileViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        ProfileRoot.Opacity = 0;
        ProfileRoot.TranslationY = 25;

        await Task.WhenAll(
            ProfileRoot.FadeTo(1, 350, Easing.CubicOut),
            ProfileRoot.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is ProfileViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }
}
