using System.Globalization;
using FixIt.Mobile.Constants;

namespace FixIt.Mobile.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? string.Empty;
        return status switch
        {
            AppConstants.StatusNew => Application.Current?.Resources["StatusNew"] ?? Colors.LightBlue,
            AppConstants.StatusInProgress => Application.Current?.Resources["StatusInProgress"] ?? Colors.Orange,
            AppConstants.StatusResolved => Application.Current?.Resources["StatusResolved"] ?? Colors.Green,
            AppConstants.StatusCritical => Application.Current?.Resources["StatusCritical"] ?? Colors.Red,
            _ => Application.Current?.Resources["OnSurfaceMuted"] ?? Colors.Gray
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AppConstants.StatusNew;
    }
}
