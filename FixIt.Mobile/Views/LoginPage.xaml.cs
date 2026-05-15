using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LoginViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        LogoBlock.Scale = 0.8;
        LogoBlock.Opacity = 0;
        FormBlock.Opacity = 0;
        FormBlock.TranslationY = 20;

        await Task.WhenAll(
            LogoBlock.FadeTo(1, 400, Easing.CubicOut),
            LogoBlock.ScaleTo(1.0, 500, Easing.SpringOut));

        await Task.WhenAll(
            FormBlock.FadeTo(1, 350, Easing.CubicOut),
            FormBlock.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is LoginViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }
}
