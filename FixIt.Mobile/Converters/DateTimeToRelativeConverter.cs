using System.Globalization;

namespace FixIt.Mobile.Converters;

public class DateTimeToRelativeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
        {
            return "just now";
        }

        var utcDate = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        var delta = DateTime.UtcNow - utcDate;

        if (delta.TotalMinutes < 1)
        {
            return "just now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)delta.TotalMinutes)} minute{Pluralize(delta.TotalMinutes)} ago";
        }

        if (delta.TotalDays < 1)
        {
            return $"{(int)delta.TotalHours} hour{Pluralize(delta.TotalHours)} ago";
        }

        if (delta.TotalDays < 2)
        {
            return "Yesterday";
        }

        if (delta.TotalDays < 7)
        {
            return $"{(int)delta.TotalDays} days ago";
        }

        return utcDate.ToLocalTime().ToString("MMM d, yyyy", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return DateTime.UtcNow;
    }

    private static string Pluralize(double value)
    {
        return Math.Round(value) == 1 ? string.Empty : "s";
    }
}
