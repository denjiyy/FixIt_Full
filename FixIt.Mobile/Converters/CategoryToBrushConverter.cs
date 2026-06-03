using System.Globalization;

namespace FixIt.Mobile.Converters;

// Produces a diagonal two-tone gradient tinted by issue category, used as the
// placeholder "photo" backdrop for feed posts that have no real image yet.
// Mirrors the design system's category hue map; unknown categories hash to a
// stable hue so each category keeps a consistent colour.
public class CategoryToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hue = CategoryVisuals.HueFor(value?.ToString());
        var top = Color.FromHsla(hue / 360.0, 0.46, 0.52);
        var bottom = Color.FromHsla(hue / 360.0, 0.46, 0.44);

        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            [
                new GradientStop(top, 0f),
                new GradientStop(bottom, 1f)
            ]
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Empty;
    }
}
