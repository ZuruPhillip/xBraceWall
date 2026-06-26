using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CncWallStation.Views;

/// <summary>
/// OPC 质量状态到颜色的转换器
/// </summary>
public class OpcQualityColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Good" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            "Bad" => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
            "Uncertain" => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
            _ => new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
