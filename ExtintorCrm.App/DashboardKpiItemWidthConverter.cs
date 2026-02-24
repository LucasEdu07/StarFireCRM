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
            return 290d;
        }
        // Accounts for window chrome + tab padding/margins so cards keep comfortable density.
        var usable = Math.Max(900d, width - 170d);
        var columns = usable switch
        {
            < 1220d => 2d,
            < 1720d => 3d,
            < 2140d => 4d,
            _ => 5d
        };

        const double itemGap = 14d;
        var calculated = (usable - ((columns - 1d) * itemGap)) / columns;

        // Prioritize label readability and avoid text clipping/wrapping in normal window mode.
        return Math.Clamp(Math.Floor(calculated), 262d, 340d);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
