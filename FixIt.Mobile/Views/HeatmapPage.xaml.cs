using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class HeatmapPage : ContentPage
{
    public HeatmapPage(HeatmapViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is HeatmapViewModel vm)
            await vm.OnAppearingAsync();
    }
}
