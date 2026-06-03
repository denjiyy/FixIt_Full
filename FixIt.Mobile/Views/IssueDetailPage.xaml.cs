using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class IssueDetailPage : ContentPage
{
    public IssueDetailPage(IssueDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        SizeChanged += OnPageSizeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is IssueDetailViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }

        DetailRoot.Opacity = 0;
        DetailRoot.TranslationY = 24;
        await Task.WhenAll(
            DetailRoot.FadeTo(1, 350, Easing.CubicOut),
            DetailRoot.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is IssueDetailViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }

    // Keep the hero photo a square that tracks the device width.
    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width > 0)
        {
            PhotoHost.HeightRequest = Math.Min(Width, 460);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnUpvoteClicked(object? sender, EventArgs e)
    {
        await AnimateVoteButtonAsync(UpvoteButton);
        if (BindingContext is IssueDetailViewModel viewModel)
        {
            await viewModel.VoteCommand.ExecuteAsync(true);
        }
    }

    private static async Task AnimateVoteButtonAsync(VisualElement button)
    {
        await button.ScaleTo(1.12, 100, Easing.SpringOut);
        await button.ScaleTo(1.0, 100, Easing.SpringOut);
    }
}
