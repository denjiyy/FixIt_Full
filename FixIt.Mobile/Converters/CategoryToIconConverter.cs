using System.Globalization;

namespace FixIt.Mobile.Converters;

// Maps an issue category string to the matching glyph asset (cat_*.png, generated
// from the SVGs in Resources/Images). Mirrors CategoryToBrushConverter's hue map so
// the Explore grid and Report picker show a consistent icon per category. Unknown
// categories fall back to the generic hazard glyph.
public class CategoryToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return CategoryVisuals.IconFor(value?.ToString());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Empty;
    }
}
