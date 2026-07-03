using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DisplayDeck.Core.Models;

namespace DisplayDeck.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public sealed class OrientationToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DisplayOrientation o
            ? o switch
            {
                DisplayOrientation.Landscape => "Landscape",
                DisplayOrientation.Portrait => "Portrait",
                DisplayOrientation.LandscapeFlipped => "Landscape (flipped)",
                DisplayOrientation.PortraitFlipped => "Portrait (flipped)",
                _ => o.ToString(),
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
