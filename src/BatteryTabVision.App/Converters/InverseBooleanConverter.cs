using System.Globalization;
using System.Windows.Data;

namespace BatteryTabVision.App.Converters;

/// <summary>
/// bool → bool 取反转换器：IsBusy=true 时 IsEnabled=false，防止按钮重复点击。
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}
