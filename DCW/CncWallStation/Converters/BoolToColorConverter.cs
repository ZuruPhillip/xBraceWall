using System;
using System.Globalization;
using System.Windows.Data;

namespace CncWallStation.Converters
{
    /// <summary>
    /// 布尔值转颜色转换器
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public string TrueColor { get; set; } = "#4CAF50";
        public string FalseColor { get; set; } = "#F44336";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? TrueColor : FalseColor;

            return FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
