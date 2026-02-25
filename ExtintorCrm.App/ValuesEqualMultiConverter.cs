using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExtintorCrm.App;

public sealed class ValuesEqualMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2)
        {
            return false;
        }

        var left = values[0];
        var right = values[1];

        if (left == DependencyProperty.UnsetValue || right == DependencyProperty.UnsetValue)
        {
            return false;
        }

        if (left is null || right is null)
        {
            return Equals(left, right);
        }

        if (TryToDecimal(left, out var leftNumber) && TryToDecimal(right, out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        return string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static bool TryToDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case decimal decimalValue:
                result = decimalValue;
                return true;
            case byte byteValue:
                result = byteValue;
                return true;
            case sbyte sbyteValue:
                result = sbyteValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            case float floatValue:
                result = (decimal)floatValue;
                return true;
            case double doubleValue:
                result = (decimal)doubleValue;
                return true;
            case string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }
}
