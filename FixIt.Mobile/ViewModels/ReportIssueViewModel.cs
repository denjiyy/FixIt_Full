using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Services;

namespace FixIt.Mobile.ViewModels;

public partial class ReportIssueViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    public ReportIssueViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _photoFileName = "No photo selected.";

    private Stream _photoStream = Stream.Null;

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        var photo = await MediaPicker.Default.CapturePhotoAsync();
        if (photo != null)
        {
            await using var stream = await photo.OpenReadAsync();
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            _photoStream.Dispose();
            _photoStream = memoryStream;
            PhotoFileName = photo.FileName;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        await _apiService.ReportIssueAsync(Title, Description, _photoStream);
    }
}
