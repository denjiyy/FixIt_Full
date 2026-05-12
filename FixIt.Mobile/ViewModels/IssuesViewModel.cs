using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;

namespace FixIt.Mobile.ViewModels;

public partial class IssuesViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    public IssuesViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    public ObservableCollection<Issue> Issues { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task LoadIssuesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var issues = await _apiService.GetIssuesAsync();

            Issues.Clear();
            foreach (var issue in issues)
            {
                Issues.Add(issue);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
