using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }

    private void OnAnonymousToggled(object? sender, ToggledEventArgs e)
    {
        if (_viewModel.ToggleAnonymousCommand.CanExecute(e.Value))
        {
            _viewModel.ToggleAnonymousCommand.Execute(e.Value);
        }
    }
}
