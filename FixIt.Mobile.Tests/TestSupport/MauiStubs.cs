using System.Globalization;

namespace FixIt.Mobile
{
    public static class App
    {
        public static event EventHandler? Resumed
        {
            add { }
            remove { }
        }
    }

    public static class MainThread
    {
        public static void BeginInvokeOnMainThread(Action action)
        {
            action();
        }
    }
}

namespace FixIt.Mobile.Converters
{
    public interface IValueConverter
    {
        object Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
        object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
    }
}

namespace FixIt.Mobile.Services
{
    public static class HapticService
    {
        public static void Click()
        {
        }

        public static void LongPress()
        {
        }
    }
}

namespace FixIt.Mobile.ViewModels
{
    public class Shell
    {
        public static Shell Current { get; set; } = new();

        public string LastRoute { get; private set; } = string.Empty;

        public Task GoToAsync(string route)
        {
            LastRoute = route;
            return Task.CompletedTask;
        }

        public Task<bool> DisplayAlert(string title, string message, string accept, string cancel)
        {
            return Task.FromResult(true);
        }

        public Task<string?> DisplayActionSheet(string title, string? cancel, string? destruction, params string[] buttons)
        {
            return Task.FromResult<string?>(cancel);
        }
    }

    public static class MainThread
    {
        public static Task InvokeOnMainThreadAsync(Func<Task> action)
        {
            return action();
        }

        public static void BeginInvokeOnMainThread(Action action)
        {
            action();
        }
    }

    public class ImageSource
    {
        public static ImageSource FromStream(Func<Stream> stream)
        {
            using var _ = stream();
            return new ImageSource();
        }
    }

    public sealed class MediaPickerOptions
    {
        public string? Title { get; set; }
    }

    public sealed class MediaPicker
    {
        public static MediaPicker Default { get; } = new();

        public bool IsCaptureSupported { get; set; }

        public Task<FileResult?> CapturePhotoAsync(MediaPickerOptions options)
        {
            return Task.FromResult<FileResult?>(null);
        }

        public Task<FileResult?> PickPhotoAsync(MediaPickerOptions options)
        {
            return Task.FromResult<FileResult?>(null);
        }
    }

    public sealed class FileResult
    {
        public string FileName { get; init; } = "issue-photo.jpg";
        public string ContentType { get; init; } = "image/jpeg";

        public Task<Stream> OpenReadAsync()
        {
            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        }
    }

    public sealed class HtmlWebViewSource
    {
        public string Html { get; set; } = string.Empty;
    }

    public sealed class ShareTextRequest
    {
        public string? Title { get; set; }
        public string? Text { get; set; }
        public string? Subject { get; set; }
        public string? Uri { get; set; }
    }

    public sealed class Share
    {
        public static Share Default { get; } = new();

        public Task RequestAsync(ShareTextRequest request)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class Location
    {
        public double Latitude { get; init; }
        public double Longitude { get; init; }
    }

    public enum GeolocationAccuracy
    {
        Lowest, Low, Medium, High, Best
    }

    public sealed class GeolocationRequest
    {
        public GeolocationRequest(GeolocationAccuracy accuracy, TimeSpan timeout)
        {
            Accuracy = accuracy;
            Timeout = timeout;
        }

        public GeolocationAccuracy Accuracy { get; }
        public TimeSpan Timeout { get; }
    }

    public sealed class Geolocation
    {
        public static Geolocation Default { get; } = new();

        public Task<Location?> GetLocationAsync(GeolocationRequest request, CancellationToken ct = default)
        {
            return Task.FromResult<Location?>(null);
        }
    }

    public class FeatureNotSupportedException : Exception
    {
        public FeatureNotSupportedException() : base() { }
    }

    public class FeatureNotEnabledException : Exception
    {
        public FeatureNotEnabledException() : base() { }
    }

    public class PermissionException : Exception
    {
        public PermissionException() : base() { }
    }

    public enum PermissionStatus
    {
        Unknown, Denied, Disabled, Granted, Restricted
    }

    public abstract class BasePermission
    {
        public virtual Task<PermissionStatus> CheckStatusAsync() => Task.FromResult(PermissionStatus.Granted);
        public virtual Task<PermissionStatus> RequestAsync() => Task.FromResult(PermissionStatus.Granted);
    }

    public static class Permissions
    {
        public sealed class LocationWhenInUse : BasePermission { }

        public static Task<PermissionStatus> CheckStatusAsync<T>() where T : BasePermission, new()
            => new T().CheckStatusAsync();

        public static Task<PermissionStatus> RequestAsync<T>() where T : BasePermission, new()
            => new T().RequestAsync();
    }
}

namespace FixIt.Mobile.Services
{
    using FixIt.Mobile.Models;
    using FixIt.Mobile.ViewModels;

    public static class MapHtmlBuilder
    {
        public static HtmlWebViewSource BuildIssueMap(Issue issue) => new();
        public static HtmlWebViewSource BuildHazardMap(
            IEnumerable<SafetyHazard> hazards,
            double centerLatitude = 42.6977,
            double centerLongitude = 23.3219,
            int zoom = 13,
            string reportPinLabel = "Drag to adjust") => new();
        public static HtmlWebViewSource BuildPickerMap(double latitude, double longitude, int zoom = 15) => new();
    }
}
