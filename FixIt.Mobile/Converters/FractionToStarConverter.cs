using System.Globalization;

namespace FixIt.Mobile.Converters;

// Turns a 0..1 fraction into a star GridLength so a two-column Grid can render a
// rounded progress fill. ConverterParameter "rest" returns the complement (1 - f),
// used for the unfilled remainder column.
public class FractionToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = 0d;
        try
        {
            fraction = System.Convert.ToDouble(value ?? 0d, CultureInfo.InvariantCulture);
        }
        catch
        {
            fraction = 0d;
        }

        fraction = Math.Clamp(fraction, 0d, 1d);
        var rest = string.Equals(parameter?.ToString(), "rest", StringComparison.OrdinalIgnoreCase);
        var portion = rest ? 1d - fraction : fraction;
        return new GridLength(Math.Max(portion, 0d), GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return 0d;
    }
}
