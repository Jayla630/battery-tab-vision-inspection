using System.Globalization;
using System.Windows.Data;

namespace BatteryTabVision.App.Converters;

/// <summary>
/// bool → Brush（Foreground）或 string（"OK"/"NG"）的双用途转换器。
/// ConverterParameter 留空 → 返回 Brush(Green/Red)；ConverterParameter="text" → 返回 "OK"/"NG"。
/// </summary>
[ValueConversion(typeof(bool), typeof(object))]
public sealed class BoolToOkNgColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isOk = value is bool b && b;
        string mode = parameter as string ?? "color";
        return mode switch
        {
            "text" => isOk ? "OK" : "NG",
            _ => isOk ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red,
        };
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
