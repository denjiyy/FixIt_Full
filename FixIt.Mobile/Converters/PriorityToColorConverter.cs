using System.Globalization;

namespace FixIt.Mobile.Converters;

// Maps an issue priority string to its semantic colour (see Colors.xaml Priority* tokens).
// Used for the priority pill overlaid on feed post photos.
public class PriorityToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var priority = value?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
        var key = priority switch
        {
            "critical" => "PriorityCritical",
            "high" => "PriorityHigh",
            "medium" => "PriorityMedium",
            "low" => "PriorityLow",
            _ => "OnSurfaceMuted"
        };

        return Application.Current?.Resources[key] ?? Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Empty;
    }
}
