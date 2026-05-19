using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace BatteryTabVision.App.Converters;

[ValueConversion(typeof(string), typeof(string))]
public sealed class FileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is string path && !string.IsNullOrEmpty(path)
            ? Path.GetFileName(path)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
