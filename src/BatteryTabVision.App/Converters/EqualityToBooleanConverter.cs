using System;
using System.Globalization;
using System.Windows.Data;

namespace BatteryTabVision.App.Converters;

public class EqualityToBooleanConverter : IMultiValueConverter
{
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
