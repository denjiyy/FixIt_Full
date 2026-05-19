using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class ReportIssueViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private byte[]? _photoBytes;
    private string _photoFileName = "issue-photo.jpg";
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public ReportIssueViewModel(IApiService api, IAuthService auth, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _auth = auth;
        _analytics = analytics;
        _performance = performance;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private ImageSource? _capturedPhotoSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _hasPhoto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _isSubmitting;

    [ObservableProperty]
    private string _titleError = string.Empty;

    [ObservableProperty]
    private string _descriptionError = string.Empty;

    [ObservableProperty]
    private string _locationError = string.Empty;

    [ObservableProperty]
    private string _photoError = string.Empty;

    [ObservableProperty]
    private string _submitError = string.Empty;

    [ObservableProperty]
    private bool _submissionSucceeded;

    public bool CanSubmit => HasPhoto && !string.IsNullOrWhiteSpace(Title) && !IsSubmitting;

    public bool HasTitleError => !string.IsNullOrWhiteSpace(TitleError);

    public bool HasDescriptionError => !string.IsNullOrWhiteSpace(DescriptionError);

    public bool HasLocationError => !string.IsNullOrWhiteSpace(LocationError);

    public bool HasPhotoError => !string.IsNullOrWhiteSpace(PhotoError);

    public bool HasSubmitError => !string.IsNullOrWhiteSpace(SubmitError);

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("ReportIssue");
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    partial void OnTitleChanged(string value)
    {
        if (HasTitleError)
        {
            TitleError = string.Empty;
        }
    }

    partial void OnDescriptionChanged(string value)
    {
        if (HasDescriptionError)
        {
            DescriptionError = string.Empty;
        }
    }

    partial void OnLocationChanged(string value)
    {
        if (HasLocationError)
        {
            LocationError = string.Empty;
        }
    }

    partial void OnTitleErrorChanged(string value) => OnPropertyChanged(nameof(HasTitleError));

    partial void OnDescriptionErrorChanged(string value) => OnPropertyChanged(nameof(HasDescriptionError));

    partial void OnLocationErrorChanged(string value) => OnPropertyChanged(nameof(HasLocationError));

    partial void OnPhotoErrorChanged(string value) => OnPropertyChanged(nameof(HasPhotoError));

    partial void OnSubmitErrorChanged(string value) => OnPropertyChanged(nameof(HasSubmitError));

    [RelayCommand]
    private async Task TakePhotoAsync(CancellationToken ct)
    {
        HapticService.Click();
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                PhotoError = LocalizationService.Get("Report_CameraUnavailable");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = LocalizationService.Get("Report_PhotoPlaceholder")
            });

            if (photo == null)
            {
                return;
            }

            _photoFileName = string.IsNullOrWhiteSpace(photo.FileName) ? "issue-photo.jpg" : photo.FileName;
            await using var stream = await photo.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, ct);
            _photoBytes = memory.ToArray();

            CapturedPhotoSource = ImageSource.FromStream(() => new MemoryStream(_photoBytes));
            HasPhoto = true;
            PhotoError = string.Empty;
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "photo_cancelled" });
        }
        catch (Exception ex)
        {
            PhotoError = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync(CancellationToken ct)
    {
        HapticService.Click();
        if (!_auth.IsLoggedIn)
        {
            await Shell.Current.GoToAsync(AppConstants.RouteSignInTabAbsolute);
            return;
        }

        if (!Validate())
        {
            return;
        }

        try
        {
            IsSubmitting = true;
            SubmitError = string.Empty;

            using (_performance.StartTrace("ReportIssue"))
            {
                // FIX B-03: dispose the upload stream only after the awaited HttpClient call has completed.
                var stream = new MemoryStream(_photoBytes ?? []);
                ApiResult result;
                try
                {
                    result = await _api.ReportIssueAsync(new ReportIssueRequest
                    {
                        Title = Title.Trim(),
                        Description = Description.Trim(),
                        Location = Location.Trim(),
                        PhotoStream = stream,
                        PhotoFileName = _photoFileName
                    }, ct);
                }
                finally
                {
                    stream.Dispose();
                }

                ct.ThrowIfCancellationRequested();

                if (result.Success)
                {
                    HapticService.LongPress();
                    await _analytics.TrackEvent("issue_reported");
                    SubmissionSucceeded = true;
                    ClearForm();
                    await Task.Delay(800, ct);
                    await Shell.Current.GoToAsync(AppConstants.RouteHome);
                }
                else
                {
                    SubmitError = result.Error ?? LocalizationService.Get("Report_Error");
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "report_cancelled" });
        }
        catch (Exception ex)
        {
            SubmitError = LocalizationService.Get("Common_Error_Generic");
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    public void SetPhotoForTesting(byte[] bytes)
    {
        _photoBytes = bytes;
        HasPhoto = bytes.Length > 0;
        CapturedPhotoSource = HasPhoto ? ImageSource.FromStream(() => new MemoryStream(bytes)) : null;
    }

    private bool Validate()
    {
        TitleError = string.Empty;
        DescriptionError = string.Empty;
        LocationError = string.Empty;
        PhotoError = string.Empty;
        SubmitError = string.Empty;

        if (_photoBytes == null || _photoBytes.Length == 0)
        {
            PhotoError = LocalizationService.Get("Report_PhotoPlaceholder");
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            TitleError = LocalizationService.Get("Report_Error_TitleRequired");
        }
        else if (Title.Length > MobileSettings.MaxTitleLength)
        {
            TitleError = LocalizationService.Get("Report_Error_TitleLength");
        }

        if (Description.Length > MobileSettings.MaxDescriptionLength)
        {
            DescriptionError = LocalizationService.Get("Report_Error_DescriptionLength");
        }

        if (Location.Length > MobileSettings.MaxLocationLength)
        {
            LocationError = LocalizationService.Get("Report_Error_LocationLength");
        }

        return !HasTitleError && !HasDescriptionError && !HasLocationError && !HasPhotoError;
    }

    private void ClearForm()
    {
        Title = string.Empty;
        Description = string.Empty;
        Location = string.Empty;
        HasPhoto = false;
        CapturedPhotoSource = null;
        _photoBytes = null;
        _photoFileName = "issue-photo.jpg";
    }

    private void CancelAndRenew()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
