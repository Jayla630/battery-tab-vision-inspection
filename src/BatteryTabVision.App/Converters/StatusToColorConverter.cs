using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BatteryTabVision.App.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status && !string.IsNullOrEmpty(status))
        {
            if (status.Contains("失败") || status.Contains("错误"))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
            }
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
