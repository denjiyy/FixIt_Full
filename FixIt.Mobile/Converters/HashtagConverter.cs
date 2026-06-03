using System.Globalization;

namespace FixIt.Mobile.Converters;

// Renders a tag as a hashtag (e.g. "bikelane" -> "#bikelane") for feed post chips.
public class HashtagConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var tag = value?.ToString()?.Trim() ?? string.Empty;
        if (tag.Length == 0)
        {
            return string.Empty;
        }

        return tag.StartsWith('#') ? tag : "#" + tag;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Empty;
    }
}
