using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is HomeViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        ResetSection(HeroBanner);
        ResetSection(StatsSection);
        ResetSection(QuickActionsSection);
        ResetSection(RecentSection);

        await AnimateSectionAsync(HeroBanner);
        await Task.Delay(150);
        await AnimateSectionAsync(StatsSection);
        await Task.Delay(150);
        await AnimateSectionAsync(QuickActionsSection);
        await Task.Delay(150);
        await AnimateSectionAsync(RecentSection);
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is HomeViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }

    private async void OnIssueTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TapGestureRecognizer { Parent: VisualElement element })
        {
            await element.ScaleTo(0.97, 80, Easing.CubicOut);
            await element.ScaleTo(1.0, 100, Easing.CubicOut);
        }

        if (BindingContext is HomeViewModel viewModel && e.Parameter is string issueId)
        {
            await viewModel.NavigateToIssueCommand.ExecuteAsync(issueId);
        }
    }

    private static void ResetSection(VisualElement element)
    {
        element.Opacity = 0;
        element.TranslationY = 30;
    }

    private static Task AnimateSectionAsync(VisualElement element)
    {
        return Task.WhenAll(
            element.FadeTo(1, 400, Easing.CubicOut),
            element.TranslateTo(0, 0, 400, Easing.CubicOut));
    }
}
