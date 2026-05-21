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
    }

    public sealed class FileResult
    {
        public string FileName { get; init; } = "issue-photo.jpg";

        public Task<Stream> OpenReadAsync()
        {
            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        }
    }
}
