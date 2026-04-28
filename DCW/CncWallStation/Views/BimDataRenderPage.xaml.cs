using CncWallStation.ViewModels;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;

namespace CncWallStation.Views
{
    /// <summary>
    /// BimDataRenderPage.xaml 的交互逻辑
    /// - 初始化 WebView2
    /// - 向 ViewModel 注入 JS 执行委托
    /// - 通过 MVVM 命令驱动页面行为
    /// </summary>
    public partial class BimDataRenderPage : Page
    {
        private readonly BimDataRenderViewModel _viewModel;

        public BimDataRenderPage()
        {
            InitializeComponent();

            // 创建 ViewModel 并绑定
            _viewModel = new BimDataRenderViewModel();
            DataContext = _viewModel;

            // ── 向 ViewModel 注入 WebView2 功能委托 ──
            // ViewModel 不直接持有 WebView2，而是通过委托调用

            // 注入：导航到 HTML 文件
            _viewModel.NavigateToHtml = () =>
            {
                var uri = new Uri(_viewModel.HtmlFilePath);
                BimWebView.CoreWebView2?.Navigate(uri.AbsoluteUri);
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
        }

        /// <summary>异步初始化 WebView2 CoreWebView2 环境</summary>
        private async void InitializeWebViewAsync()
        {
            try
            {
                await BimWebView.EnsureCoreWebView2Async(null);
                // 初始化成功后，WebView2 准备就绪，等待用户点击"加载渲染"
            }
            catch (Exception ex)
            {
                _viewModel.OnRenderFailed($"WebView2 初始化失败: {ex.Message}");
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
                _viewModel.OnRenderFailed("CoreWebView2 初始化未成功");
                return;
            }

            // 允许访问本地文件（file:// 协议）
            BimWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            BimWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;

            // 监听 WebView2 发送的消息（HTML → WPF 通信）
            BimWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        }

        /// <summary>导航开始事件</summary>
        private void BimWebView_NavigationStarting(
            object sender,
            CoreWebView2NavigationStartingEventArgs e)
        {
            _viewModel.IsLoading = true;
            _viewModel.StatusMessage = $"🔄 正在导航到: {e.Uri}";
        }

        /// <summary>导航完成事件</summary>
        private void BimWebView_NavigationCompleted(
            object sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                // 隐藏占位遮罩，显示 WebView2 内容
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                _viewModel.OnRenderLoaded();
            }
            else
            {
                _viewModel.OnRenderFailed($"导航失败 (HTTP {e.HttpStatusCode})");
            }
        }

        /// <summary>接收来自 HTML/JS 的消息（可用于双向通信）</summary>
        private void OnWebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                string message = args.TryGetWebMessageAsString();

                // 解析来自 Three.js 页面的消息
                if (message.StartsWith("wallInfo:"))
                {
                    _viewModel.WallInfo = message.Substring(9);
                }
                else if (message.StartsWith("status:"))
                {
                    _viewModel.StatusMessage = message.Substring(7);
                }
                // 可根据需要扩展更多消息类型
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"⚠️ 消息解析错误: {ex.Message}";
            }
        }
    }
}

