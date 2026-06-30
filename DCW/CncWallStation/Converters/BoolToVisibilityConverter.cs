using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CncWallStation.Converters
{
    /// <summary>
    /// 布尔值反转为 Visibility 转换器
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            bool invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
            return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
