using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class EditIssuePage : ContentPage
{
    public EditIssuePage(EditIssueViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EditIssueViewModel vm)
            await vm.OnAppearingAsync();
    }
}
