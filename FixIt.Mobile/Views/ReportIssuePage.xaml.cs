using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class ReportIssuePage : ContentPage
{
    public ReportIssuePage(ReportIssueViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
