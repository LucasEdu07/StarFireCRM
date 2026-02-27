using System;
using System.Globalization;
using System.Windows.Data;

namespace ExtintorCrm.App;

public class DoubleLessThanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double source || double.IsNaN(source))
        {
            return false;
        }

        if (parameter is null)
        {
            return false;
        }

        var raw = parameter.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) &&
            !double.TryParse(raw, NumberStyles.Float, culture, out threshold))
        {
            return false;
        }

        return source < threshold;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
