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
            return 250d;
        }
        // Accounts for window chrome + tab padding/margins while keeping KPI cards compact.
        var usable = Math.Max(820d, width - 170d);
        var columns = usable switch
        {
            < 1180d => 2d,
            < 1560d => 3d,
            < 1940d => 4d,
            < 2320d => 5d,
            _ => 6d
        };

        const double itemGap = 12d;
        var calculated = (usable - ((columns - 1d) * itemGap)) / columns;

        // Keep cards tight and aligned while preserving label readability.
        return Math.Clamp(Math.Floor(calculated), 228d, 296d);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
