using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class CitiesPage : ContentPage
{
    public CitiesPage(CitiesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CitiesViewModel vm)
            await vm.OnAppearingAsync();
    }
}
