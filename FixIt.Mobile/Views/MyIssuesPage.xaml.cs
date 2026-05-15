using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class MyIssuesPage : ContentPage
{
    public MyIssuesPage(MyIssuesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        MyIssuesCollection.Opacity = 0;
        MyIssuesCollection.TranslationY = 24;

        if (BindingContext is MyIssuesViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        await Task.WhenAll(
            MyIssuesCollection.FadeTo(1, 350, Easing.CubicOut),
            MyIssuesCollection.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is MyIssuesViewModel viewModel)
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

        if (BindingContext is MyIssuesViewModel viewModel && e.Parameter is string issueId)
        {
            await viewModel.NavigateToIssueCommand.ExecuteAsync(issueId);
        }
    }

    private async void OnDeleteIssueInvoked(object? sender, EventArgs e)
    {
        if (BindingContext is MyIssuesViewModel viewModel && sender is SwipeItem { CommandParameter: string issueId })
        {
            await viewModel.DeleteIssueCommand.ExecuteAsync(issueId);
        }
    }
}
