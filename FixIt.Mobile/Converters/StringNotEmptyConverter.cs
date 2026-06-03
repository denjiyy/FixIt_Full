using System.Globalization;

namespace FixIt.Mobile.Converters;

// True when the bound string has non-whitespace content. Used to hide empty
// priority/status pills on feed posts.
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value?.ToString());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Empty;
    }
}
