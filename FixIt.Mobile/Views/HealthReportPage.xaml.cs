using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class HealthReportPage : ContentPage
{
    public HealthReportPage(HealthReportViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is HealthReportViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        HealthRoot.Opacity = 0;
        HealthRoot.TranslationY = 24;
        await Task.WhenAll(
            HealthRoot.FadeTo(1, 350, Easing.CubicOut),
            HealthRoot.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is HealthReportViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }
}
