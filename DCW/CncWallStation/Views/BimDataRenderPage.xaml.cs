using CncWallStation.Localization;
using CncWallStation.ViewModels;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;

namespace CncWallStation.Views
{
    /// <summary>
    /// BimDataRenderPage.xaml 的交互逻辑
    /// - 通过 DI 注入 ViewModel
    /// - 管理 WebView2 生命周期
    /// - 分发 HTML postMessage 到 ViewModel
    /// </summary>
    public partial class BimDataRenderPage : Page
    {
        private readonly BimDataRenderViewModel _viewModel;

        /// <summary>
        /// 构造函数（通过 DI 注入 ViewModel）
        /// </summary>
        public BimDataRenderPage(BimDataRenderViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // ── 向 ViewModel 注入 WebView2 功能委托 ──

            // 注入：导航到 HTML 文件（等待 CoreWebView2 就绪）
            _viewModel.NavigateToHtml = async () =>
            {
                // 确保 CoreWebView2 已初始化
                try
                {
                    await BimWebView.EnsureCoreWebView2Async(null);
                }
                catch (Exception ex)
                {
                    _viewModel.OnPageFailed($"WebView2 初始化失败: {ex.Message}");
                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var uri = new Uri(_viewModel.HtmlFilePath);
                    BimWebView.CoreWebView2.Navigate(uri.AbsoluteUri);
                });
            };

            // 注入：执行 JavaScript
            _viewModel.ExecuteScriptAsync = async (script) =>
            {
                if (BimWebView.CoreWebView2 != null)
                {
                    await BimWebView.CoreWebView2.ExecuteScriptAsync(script);
                }
            };

            // 初始化 WebView2 运行时
            InitializeWebViewAsync();

            // 订阅语言切换事件，同步 HTML 3D 视图语言
            LocalizationService.Instance.CultureChanged += OnCultureChanged;
        }

        private async void OnCultureChanged(object? sender, string cultureName)
        {
            var langCode = cultureName == "zh-CN" ? "zh" : "en";
            if (BimWebView?.CoreWebView2 != null)
            {
                await BimWebView.CoreWebView2.ExecuteScriptAsync(
                    $"setLanguage('{langCode}')");
            }
        }

        /// <summary>异步初始化 WebView2 CoreWebView2 环境</summary>
        private async void InitializeWebViewAsync()
        {
            try
            {
                await BimWebView.EnsureCoreWebView2Async(null);
            }
            catch (Exception ex)
            {
                _viewModel.OnPageFailed($"WebView2 初始化失败: {ex.Message}");
                MessageBox.Show(
                    $"WebView2 运行时初始化失败！\n\n" +
                    $"请确保已安装 Microsoft Edge WebView2 Runtime。\n\n" +
                    $"错误信息：{ex.Message}",
                    "WebView2 初始化错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────────
        //  WebView2 事件处理
        // ──────────────────────────────────────────────────

        /// <summary>CoreWebView2 初始化完成事件</summary>
        private void BimWebView_CoreWebView2InitializationCompleted(
            object sender,
            CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                _viewModel.OnPageFailed("CoreWebView2 初始化未成功");
                return;
            }

            // 启用 WebMessage 通信（postMessage）
            BimWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            BimWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;

            // 监听 HTML → WPF 消息
            BimWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        }

        /// <summary>导航开始事件</summary>
        private void BimWebView_NavigationStarting(
            object sender,
            CoreWebView2NavigationStartingEventArgs e)
        {
            _viewModel.IsLoading = true;
        }

        /// <summary>导航完成事件</summary>
        private void BimWebView_NavigationCompleted(
            object sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                // 隐藏占位遮罩
                PlaceholderPanel.Visibility = Visibility.Collapsed;

                // 同步语言到 HTML
                var langCode = LocalizationService.Instance.CurrentLanguage == "zh-CN" ? "zh" : "en";
                _ = BimWebView.CoreWebView2.ExecuteScriptAsync($"setLanguage('{langCode}')");

                // 通知 ViewModel 页面加载完成（将触发数据注入）
                _viewModel.OnPageLoaded();
            }
            else
            {
                _viewModel.OnPageFailed($"导航失败 (HTTP {e.HttpStatusCode})");
            }
        }

        /// <summary>接收来自 HTML/JS 的 postMessage</summary>
        private void OnWebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var message = args.TryGetWebMessageAsString();

                // 分发到 ViewModel 统一处理
                // 在 UI 线程上调用
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _viewModel.HandleWebMessage(message);
                });
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"⚠️ 消息解析错误: {ex.Message}";
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
