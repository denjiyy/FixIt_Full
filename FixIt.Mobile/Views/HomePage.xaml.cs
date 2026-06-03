using FixIt.Mobile.Models;
using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Feed card actions are wired in code-behind (not cross-context x:Reference command
    // bindings, which proved unreliable from inside the CollectionView item template).
    // The bound Issue comes from the tapped control's BindingContext.
    private async void OnCommentClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject control && control.BindingContext is Issue issue &&
            BindingContext is HomeViewModel viewModel)
        {
            await viewModel.NavigateToIssueCommand.ExecuteAsync(issue.Id);
        }
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject control && control.BindingContext is Issue issue &&
            BindingContext is HomeViewModel viewModel)
        {
            viewModel.SaveCommand.Execute(issue);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is HomeViewModel viewModel)
        {
            await viewModel.OnAppearingAsync();
        }
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is HomeViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }
}
