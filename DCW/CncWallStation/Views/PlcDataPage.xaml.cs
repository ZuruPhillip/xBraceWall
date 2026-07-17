using CncWallStation.Localization;
using CncWallStation.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CncWallStation.Views
{
    /// <summary>
    /// PLC 指令单元页面
    /// </summary>
    public partial class PlcDataPage : Page
    {
        private readonly PlcDataViewModel _viewModel;
        private bool _webViewInitialized;

        public PlcDataPage(PlcDataViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // 注入 WebView2 委托
            _viewModel.NavigateToHtml = NavigateToHtmlAsync;
            _viewModel.ExecuteScriptAsync = ExecuteScriptToWebViewAsync;

            // 初始化 WebView2
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
        }

        // ==================== WebView2 初始化 ====================

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync();
                await PlcWebView.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 初始化失败: {ex.Message}");
            }
        }

        private void PlcWebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                PlcWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webViewInitialized = true;
            }
        }

        private async void PlcWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && _viewModel.IsWallLoaded)
            {
                // 页面加载完成后，推送指令数据渲染
                await Task.Delay(500);
                // 注意：NavigationCompleted 之后 _viewModel 会自动调用 Render3DAsync
                // 此处仅作占位
            }
        }

        // ==================== WebView2 委托实现 ====================

        private async Task NavigateToHtmlAsync(string url)
        {
            if (PlcWebView.CoreWebView2 != null)
            {
                PlcWebView.CoreWebView2.Navigate(url);
            }
            else
            {
                PlcWebView.Source = new Uri(url);
            }
            await Task.CompletedTask;
        }

        private async Task ExecuteScriptToWebViewAsync(string script)
        {
            if (_webViewInitialized && PlcWebView.CoreWebView2 != null)
            {
                await PlcWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        // ==================== JS → WPF 消息 ====================

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                // 预留：处理来自 JS 的消息（如选中指令、视角变化等）
                System.Diagnostics.Debug.WriteLine($"WebView message: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView message error: {ex.Message}");
            }
        }

        // ==================== 搜索回车 ====================

        private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.SearchCommand.CanExecute(null))
            {
                _viewModel.SearchCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    // ==================== XAML 值转换器 ====================

    /// <summary>布尔值取反（PlcDataPage 专用）</summary>
    public class PlcBoolInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true;
        }
    }

    /// <summary>布尔值 → 可见性（PlcDataPage 专用，避免与 WallListPage 同名冲突）</summary>
    public class PlcBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return b ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v) return v == Visibility.Visible;
            return false;
        }
    }

    /// <summary>审核状态 → 中/英文显示文本（PlcDataPage 专用）</summary>
    public class PlcAuditStatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEn = LocalizationService.Instance.CurrentLanguage.StartsWith("en");
            if (value is int status)
                return status == 1 ? (isEn ? "Audited" : "已审核") : (isEn ? "Unaudited" : "未审核");
            return "--";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>指令条数 → 本地化格式（PlcDataPage 专用，支持中英文单位）</summary>
    public class PlcInstructionCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return string.Format(LocalizationService.Instance["Stat_InstructionUnit"], count);
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>审核状态 → 颜色（PlcDataPage 专用）</summary>
    public class PlcAuditStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                var brush = status == 1 ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Orange);
                brush.Freeze();
                return brush;
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>int → bool 转换器（PlcDataPage 专用，用于 RadioButton 绑定 int 属性）</summary>
    public class PlcIntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intVal && parameter is string paramStr && int.TryParse(paramStr, out int paramVal))
                return intVal == paramVal;
            if (value is int intVal2 && parameter is int paramVal2)
                return intVal2 == paramVal2;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is string paramStr && int.TryParse(paramStr, out int paramVal))
                return paramVal;
            if (value is bool b2 && b2 && parameter is int paramVal2)
                return paramVal2;
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
