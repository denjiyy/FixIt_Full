using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class IssuesPage : ContentPage
{
    public IssuesPage(IssuesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
