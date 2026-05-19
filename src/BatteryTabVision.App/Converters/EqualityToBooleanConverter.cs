using System;
using System.Globalization;
using System.Windows.Data;

namespace BatteryTabVision.App.Converters;

public class EqualityToBooleanConverter : IValueConverter, IMultiValueConverter
{
    // IValueConverter: Compare value with parameter
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null && value.Equals(true) ? parameter : Binding.DoNothing;
    }

    // IMultiValueConverter: Compare first two values (legacy support for MainShellView)
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        if (values[0] == null || values[1] == null) return false;
        
        return values[0].ToString() == values[1].ToString();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
