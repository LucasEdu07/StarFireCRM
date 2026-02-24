using System;
using System.Globalization;
using System.Windows.Data;

namespace ExtintorCrm.App;

public class DashboardKpiItemWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width || double.IsNaN(width) || width <= 0)
        {
            return 232d;
        }

        return width switch
        {
            < 1280 => 206d,
            < 1450 => 218d,
            < 1650 => 228d,
            < 1860 => 238d,
            _ => 248d
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
