using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class HazardModePage : ContentPage
{
    public HazardModePage(HazardModeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is HazardModeViewModel vm)
            await vm.OnAppearingAsync();
    }
}
