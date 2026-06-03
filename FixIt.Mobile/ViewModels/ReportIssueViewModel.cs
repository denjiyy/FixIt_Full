using System.Collections.ObjectModel;
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
    private const int MaxPhotos = 5;
    private const long MaxBytesPerPhoto = 10L * 1024 * 1024;
    private const double DefaultLatitude = 42.6977;   // Sofia city centre
    private const double DefaultLongitude = 23.3219;

    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;
    private bool _cameraAutoLaunched;

    public ReportIssueViewModel(IApiService api, IAuthService auth, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _auth = auth;
        _analytics = analytics;
        _performance = performance;
        Photos = new ObservableCollection<PhotoAttachment>();
        Photos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPhotos));
            OnPropertyChanged(nameof(CanAddPhoto));
        };
        CityId = AppConstants.DefaultCityId;

        Categories = new ObservableCollection<CategoryOption>
        {
            new() { Key = "road",     Label = "Road",     IconSource = "cat_road.png" },
            new() { Key = "light",    Label = "Lighting", IconSource = "cat_light.png" },
            new() { Key = "graffiti", Label = "Graffiti", IconSource = "cat_graffiti.png" },
            new() { Key = "waste",    Label = "Waste",    IconSource = "cat_waste.png" },
            new() { Key = "water",    Label = "Water",    IconSource = "cat_water.png" },
            new() { Key = "signage",  Label = "Signs",    IconSource = "cat_signage.png" },
            new() { Key = "sidewalk", Label = "Sidewalk", IconSource = "cat_sidewalk.png" },
            new() { Key = "park",     Label = "Parks",    IconSource = "cat_park.png" },
        };
    }

    public ObservableCollection<PhotoAttachment> Photos { get; }

    // Category picker + priority selector (design "report" screen). These mirror the
    // server's own AI classification, so they are presentational/confirmational and
    // pre-select from the AI suggestion when it arrives.
    public ObservableCollection<CategoryOption> Categories { get; }

    [ObservableProperty]
    private string _selectedCategoryKey = string.Empty;

    [ObservableProperty]
    private string _selectedPriority = "medium";

    public bool IsPriorityLow => SelectedPriority == "low";
    public bool IsPriorityMedium => SelectedPriority == "medium";
    public bool IsPriorityHigh => SelectedPriority == "high";
    public bool IsPriorityCritical => SelectedPriority == "critical";

    partial void OnSelectedPriorityChanged(string value)
    {
        OnPropertyChanged(nameof(IsPriorityLow));
        OnPropertyChanged(nameof(IsPriorityMedium));
        OnPropertyChanged(nameof(IsPriorityHigh));
        OnPropertyChanged(nameof(IsPriorityCritical));
    }

    [RelayCommand]
    private void SelectCategory(CategoryOption? option)
    {
        if (option is null)
        {
            return;
        }

        HapticService.Click();
        SelectedCategoryKey = option.Key;
        foreach (var category in Categories)
        {
            category.IsSelected = category.Key == option.Key;
        }
    }

    [RelayCommand]
    private void SelectPriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return;
        }

        HapticService.Click();
        SelectedPriority = priority.Trim().ToLowerInvariant();
    }

    private static string CategoryKeyFor(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        var c = category.ToLowerInvariant();
        if (c.Contains("pothole") || c.Contains("road")) return "road";
        if (c.Contains("light")) return "light";
        if (c.Contains("graffiti") || c.Contains("vandal")) return "graffiti";
        if (c.Contains("trash") || c.Contains("waste") || c.Contains("dump") || c.Contains("litter")) return "waste";
        if (c.Contains("water") || c.Contains("leak") || c.Contains("flood") || c.Contains("drain")) return "water";
        if (c.Contains("sign") || c.Contains("signal")) return "signage";
        if (c.Contains("sidewalk") || c.Contains("pavement") || c.Contains("walk")) return "sidewalk";
        if (c.Contains("park") || c.Contains("tree") || c.Contains("green") || c.Contains("garden")) return "park";
        return string.Empty;
    }

    public bool HasPhotos => Photos.Count > 0;

    public bool CanAddPhoto => Photos.Count < MaxPhotos && !IsSubmitting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _cityId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private double? _latitude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private double? _longitude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _isLocationConfirmed;

    [ObservableProperty]
    private bool _isLocating;

    [ObservableProperty]
    private HtmlWebViewSource? _mapSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyPropertyChangedFor(nameof(CanAddPhoto))]
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
    private string _submitWarning = string.Empty;

    [ObservableProperty]
    private bool _submissionSucceeded;

    [ObservableProperty]
    private string _tagsText = string.Empty;

    [ObservableProperty]
    private DraftSuggestion? _aiSuggestion;

    [ObservableProperty]
    private bool _isLoadingSuggestion;

    public bool HasAiSuggestion => AiSuggestion != null;

    partial void OnAiSuggestionChanged(DraftSuggestion? value)
    {
        OnPropertyChanged(nameof(HasAiSuggestion));
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(SelectedCategoryKey) && !string.IsNullOrWhiteSpace(value.Category))
        {
            var key = CategoryKeyFor(value.Category);
            var match = Categories.FirstOrDefault(c => c.Key == key);
            if (match is not null)
            {
                SelectCategory(match);
            }
        }

        if (!string.IsNullOrWhiteSpace(value.Priority))
        {
            SelectedPriority = value.Priority.Trim().ToLowerInvariant();
        }
    }

    public bool CanSubmit =>
        !string.IsNullOrWhiteSpace(Title)
        && Latitude.HasValue
        && Longitude.HasValue
        && IsLocationConfirmed
        && !IsSubmitting;

    public bool HasTitleError => !string.IsNullOrWhiteSpace(TitleError);
    public bool HasDescriptionError => !string.IsNullOrWhiteSpace(DescriptionError);
    public bool HasLocationError => !string.IsNullOrWhiteSpace(LocationError);
    public bool HasPhotoError => !string.IsNullOrWhiteSpace(PhotoError);
    public bool HasSubmitError => !string.IsNullOrWhiteSpace(SubmitError);
    public bool HasSubmitWarning => !string.IsNullOrWhiteSpace(SubmitWarning);
    public bool HasAddress => !string.IsNullOrWhiteSpace(Address);

    public async Task OnAppearingAsync()
    {
        await _analytics.TrackScreen("ReportIssue");
        if (!_auth.IsLoggedIn)
        {
            return;
        }

        await InitializeLocationAsync(_cts.Token);

        if (!_cameraAutoLaunched && Photos.Count == 0)
        {
            _cameraAutoLaunched = true;
            await TakePhotoAsync(_cts.Token);
        }
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
    }

    public async Task OnMapPointSelectedAsync(double latitude, double longitude, CancellationToken ct)
    {
        Latitude = latitude;
        Longitude = longitude;
        IsLocationConfirmed = true;
        LocationError = string.Empty;
        await TryReverseGeocodeAsync(latitude, longitude, ct);
    }

    partial void OnTitleChanged(string value)
    {
        if (HasTitleError) TitleError = string.Empty;
    }

    partial void OnDescriptionChanged(string value)
    {
        if (HasDescriptionError) DescriptionError = string.Empty;
        if (Title.Length >= 10 && value.Length >= 20)
            _ = FetchAiSuggestionsAsync();
    }

    private async Task FetchAiSuggestionsAsync()
    {
        if (IsLoadingSuggestion) return;
        try
        {
            IsLoadingSuggestion = true;
            AiSuggestion = await _api.GetDraftSuggestionsAsync(Title, Description);
        }
        catch { /* non-critical */ }
        finally { IsLoadingSuggestion = false; }
    }

    partial void OnAddressChanged(string value) => OnPropertyChanged(nameof(HasAddress));
    partial void OnTitleErrorChanged(string value) => OnPropertyChanged(nameof(HasTitleError));
    partial void OnDescriptionErrorChanged(string value) => OnPropertyChanged(nameof(HasDescriptionError));
    partial void OnLocationErrorChanged(string value) => OnPropertyChanged(nameof(HasLocationError));
    partial void OnPhotoErrorChanged(string value) => OnPropertyChanged(nameof(HasPhotoError));
    partial void OnSubmitErrorChanged(string value) => OnPropertyChanged(nameof(HasSubmitError));
    partial void OnSubmitWarningChanged(string value) => OnPropertyChanged(nameof(HasSubmitWarning));

    [RelayCommand]
    private async Task AddPhotoAsync(CancellationToken ct)
    {
        if (Photos.Count >= MaxPhotos)
        {
            PhotoError = LocalizationService.Get("Report_PhotoLimit");
            return;
        }

        HapticService.Click();
        if (Shell.Current is not { } shell)
        {
            await TakePhotoAsync(ct);
            return;
        }

        var action = await shell.DisplayActionSheet(
            LocalizationService.Get("Report_AddPhoto"),
            LocalizationService.Get("Report_PhotoActions_Cancel"),
            null,
            LocalizationService.Get("Report_TakePhoto"),
            LocalizationService.Get("Report_PickFromGallery"));

        if (string.Equals(action, LocalizationService.Get("Report_TakePhoto"), StringComparison.Ordinal))
        {
            await TakePhotoAsync(ct);
        }
        else if (string.Equals(action, LocalizationService.Get("Report_PickFromGallery"), StringComparison.Ordinal))
        {
            await PickPhotoAsync(ct);
        }
    }

    [RelayCommand]
    private async Task TakePhotoAsync(CancellationToken ct)
    {
        if (Photos.Count >= MaxPhotos)
        {
            PhotoError = LocalizationService.Get("Report_PhotoLimit");
            return;
        }

        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                PhotoError = LocalizationService.Get("Report_CameraUnavailable");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = LocalizationService.Get("Report_AddPhoto")
            });

            await AddPhotoFromFileResultAsync(photo, ct);
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

    [RelayCommand]
    private async Task PickPhotoAsync(CancellationToken ct)
    {
        if (Photos.Count >= MaxPhotos)
        {
            PhotoError = LocalizationService.Get("Report_PhotoLimit");
            return;
        }

        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = LocalizationService.Get("Report_AddPhoto")
            });

            await AddPhotoFromFileResultAsync(photo, ct);
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

    [RelayCommand]
    private void RemovePhoto(PhotoAttachment? photo)
    {
        if (photo is null) return;
        Photos.Remove(photo);
        PhotoError = string.Empty;
    }

    private async Task AddPhotoFromFileResultAsync(FileResult? photo, CancellationToken ct)
    {
        if (photo == null) return;

        await using var stream = await photo.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();

        if (bytes.Length > MaxBytesPerPhoto)
        {
            PhotoError = LocalizationService.Get("Report_PhotoTooLarge");
            return;
        }

        var fileName = string.IsNullOrWhiteSpace(photo.FileName)
            ? $"issue-photo-{Guid.NewGuid():N}.jpg"
            : photo.FileName;
        var contentType = string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;

        Photos.Add(new PhotoAttachment
        {
            Bytes = bytes,
            FileName = fileName,
            ContentType = contentType,
            Thumbnail = ImageSource.FromStream(() => new MemoryStream(bytes))
        });
        PhotoError = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync(CancellationToken ct)
    {
        HapticService.Click();
        if (!_auth.IsLoggedIn)
        {
            await Shell.Current.GoToAsync(AppConstants.RouteAccountTabAbsolute);
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
            SubmitWarning = string.Empty;

            using (_performance.StartTrace("ReportIssue"))
            {
                var result = await _api.ReportIssueAsync(new ReportIssueRequest
                {
                    Title = Title.Trim(),
                    Description = Description.Trim(),
                    Latitude = Latitude!.Value,
                    Longitude = Longitude!.Value,
                    CityId = string.IsNullOrWhiteSpace(CityId) ? AppConstants.DefaultCityId : CityId,
                    Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                    Photos = Photos.ToList()
                }, ct);

                ct.ThrowIfCancellationRequested();

                if (result.Success)
                {
                    HapticService.LongPress();
                    await _analytics.TrackEvent("issue_reported");
                    SubmissionSucceeded = true;
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        SubmitWarning = result.Error;
                    }
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

    public void AddPhotoForTesting(PhotoAttachment photo)
    {
        Photos.Add(photo);
    }

    public void SetCoordinatesForTesting(double lat, double lon, bool confirmed = true)
    {
        Latitude = lat;
        Longitude = lon;
        IsLocationConfirmed = confirmed;
    }

    private async Task InitializeLocationAsync(CancellationToken ct)
    {
        if (Latitude.HasValue && Longitude.HasValue && IsLocationConfirmed)
        {
            return;
        }

        try
        {
            IsLocating = true;
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            Location? location = null;
            if (status == PermissionStatus.Granted)
            {
                try
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                    location = await Geolocation.Default.GetLocationAsync(request, ct);
                }
                catch (FeatureNotSupportedException)
                {
                    // Simulator without GPS — fall back to default below.
                }
                catch (FeatureNotEnabledException)
                {
                    // Location services disabled — fall back to default below.
                }
                catch (PermissionException)
                {
                }
            }

            if (location != null)
            {
                Latitude = location.Latitude;
                Longitude = location.Longitude;
                IsLocationConfirmed = true;
                MapSource = MapHtmlBuilder.BuildPickerMap(location.Latitude, location.Longitude);
                LocationError = string.Empty;
                await TryReverseGeocodeAsync(location.Latitude, location.Longitude, ct);
            }
            else
            {
                Latitude = DefaultLatitude;
                Longitude = DefaultLongitude;
                IsLocationConfirmed = false;
                MapSource = MapHtmlBuilder.BuildPickerMap(DefaultLatitude, DefaultLongitude, zoom: 12);
                LocationError = LocalizationService.Get("Report_LocationPending");
            }
        }
        catch (Exception ex)
        {
            Latitude = DefaultLatitude;
            Longitude = DefaultLongitude;
            IsLocationConfirmed = false;
            MapSource = MapHtmlBuilder.BuildPickerMap(DefaultLatitude, DefaultLongitude, zoom: 12);
            LocationError = LocalizationService.Get("Report_LocationPending");
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "location_init" });
        }
        finally
        {
            IsLocating = false;
        }
    }

    private async Task TryReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct)
    {
        try
        {
            var result = await _api.ReverseGeocodeAsync(latitude, longitude, ct);
            if (result is null) return;

            if (!string.IsNullOrWhiteSpace(result.Address))
            {
                Address = result.Address;
            }
            if (!string.IsNullOrWhiteSpace(result.CityId))
            {
                CityId = result.CityId!;
            }
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "reverse_geocode" });
        }
    }

    private bool Validate()
    {
        TitleError = string.Empty;
        DescriptionError = string.Empty;
        LocationError = string.Empty;
        PhotoError = string.Empty;
        SubmitError = string.Empty;

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

        if (!Latitude.HasValue || !Longitude.HasValue || !IsLocationConfirmed)
        {
            LocationError = LocalizationService.Get("Report_LocationPending");
        }

        return !HasTitleError && !HasDescriptionError && !HasLocationError;
    }

    private void ClearForm()
    {
        Title = string.Empty;
        Description = string.Empty;
        Address = string.Empty;
        Photos.Clear();
        PhotoError = string.Empty;
        _cameraAutoLaunched = false;
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
