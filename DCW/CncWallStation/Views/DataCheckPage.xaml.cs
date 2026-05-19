using CncWallStation.Models.Enums;
using CncWallStation.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CncWallStation.Views
{
    public partial class DataCheckPage : System.Windows.Controls.Page
    {
        private readonly DataCheckViewModel _viewModel;

        public DataCheckPage(DataCheckViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }
    }

    // ==================== 转换器 ====================

    /// <summary>严重等级 → 颜色 Brushes</summary>
    public class SeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ErrorSeverity severity)
            {
                return severity switch
                {
                    ErrorSeverity.Critical => new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4F)),
                    ErrorSeverity.Error => new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x45)),
                    ErrorSeverity.Warning => new SolidColorBrush(Color.FromRgb(0xFA, 0xAD, 0x14)),
                    ErrorSeverity.Info => new SolidColorBrush(Color.FromRgb(0x16, 0x77, 0xFF)),
                    _ => new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99))
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>严重等级 → 中文图标文本</summary>
    public class SeverityToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ErrorSeverity severity)
                return severity.ToDisplayText();
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>得分 → 颜色</summary>
    public class ScoreToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score)
            {
                return score >= 80
                    ? new SolidColorBrush(Color.FromRgb(0x52, 0xC4, 0x1A))
                    : score >= 60
                        ? new SolidColorBrush(Color.FromRgb(0xFA, 0xAD, 0x14))
                        : new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4F));
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>IsPassed → 状态文本/颜色</summary>
    public class IsPassedToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool passed)
            {
                return passed ? "✓ 通过" : "✗ 失败";
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

}
