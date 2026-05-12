using CommunityToolkit.Mvvm.ComponentModel;

namespace FixIt.Mobile.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "FixIt";

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to the FixIt mobile experience.";
}
