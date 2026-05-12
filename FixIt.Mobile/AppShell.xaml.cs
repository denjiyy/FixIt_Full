using FixIt.Mobile.Views;

namespace FixIt.Mobile;

public partial class AppShell : Shell
{
    public AppShell(
        HomePage homePage,
        IssuesPage issuesPage,
        ReportIssuePage reportIssuePage,
        LoginPage loginPage)
    {
        InitializeComponent();

        HomeContent.Content = homePage;
        IssuesContent.Content = issuesPage;
        ReportIssueContent.Content = reportIssuePage;
        LoginContent.Content = loginPage;
    }
}
