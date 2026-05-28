using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class TagDetailPage : ContentPage
{
    public TagDetailPage(TagDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is TagDetailViewModel vm)
            await vm.OnAppearingAsync();
    }
}
