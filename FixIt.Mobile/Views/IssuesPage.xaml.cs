using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class IssuesPage : ContentPage
{
    public IssuesPage(IssuesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        IssuesCollection.Opacity = 0;
        IssuesCollection.TranslationY = 40;

        if (BindingContext is IssuesViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        await Task.WhenAll(
            IssuesCollection.FadeTo(1, 350, Easing.CubicOut),
            IssuesCollection.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is IssuesViewModel viewModel)
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

        if (BindingContext is IssuesViewModel viewModel && e.Parameter is string issueId)
        {
            await viewModel.NavigateToIssueCommand.ExecuteAsync(issueId);
        }
    }
}
