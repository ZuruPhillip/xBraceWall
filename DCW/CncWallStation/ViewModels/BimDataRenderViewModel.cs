using CncWallStation.Commands;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;


namespace CncWallStation.ViewModels
{
    /// <summary>
    /// BimDataRenderPage 的 ViewModel
    /// 使用 CommunityToolkit.Mvvm 的 ObservableObject
    /// 命令使用自定义 RelayCommand
    /// </summary>
    public partial class BimDataRenderViewModel : ObservableObject
    {
        // ────────────────────────────────────────────────
        // 可观察属性（使用 CommunityToolkit.Mvvm 的属性通知）
        // ────────────────────────────────────────────────

        private string _statusMessage = "就绪 · 等待操作";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _wallInfo = "";
        public string WallInfo
        {
            get => _wallInfo;
            set => SetProperty(ref _wallInfo, value);
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                SetProperty(ref _isLoading, value);
                // 刷新所有命令的 CanExecute 状态
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _htmlFilePath = string.Empty;
        public string HtmlFilePath
        {
            get => _htmlFilePath;
            set => SetProperty(ref _htmlFilePath, value);
        }

        // ────────────────────────────────────────────────
        // 命令：使用自定义 RelayCommand（来自 Commands 命名空间）
        // ────────────────────────────────────────────────

        /// <summary>加载/刷新 Three.js 3D 墙体渲染页面</summary>
        public RelayCommand LoadRenderCommand { get; }

        /// <summary>重置视角（向 WebView2 注入 JS）</summary>
        public RelayCommand ResetViewCommand { get; }

        /// <summary>切换砖块图层显示（向 WebView2 注入 JS）</summary>
        public RelayCommand ToggleBrickLayerCommand { get; }

        /// <summary>切换标注图层显示（向 WebView2 注入 JS）</summary>
        public RelayCommand ToggleAnnotationCommand { get; }

        /// <summary>导出截图提示</summary>
        public RelayCommand ExportCommand { get; }

        // ────────────────────────────────────────────────
        // 供 View 调用的 JS 注入回调（由 View 注册）
        // ────────────────────────────────────────────────

        /// <summary>由 View 注入：执行 JavaScript 的委托</summary>
        public Func<string, System.Threading.Tasks.Task>? ExecuteScriptAsync { get; set; }

        /// <summary>由 View 注入：重新导航的委托</summary>
        public Action? NavigateToHtml { get; set; }

        // ────────────────────────────────────────────────
        // 构造函数
        // ────────────────────────────────────────────────

        public BimDataRenderViewModel()
        {
            // 解析 HTML 文件路径（输出目录下的 Resources 文件夹）
            HtmlFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                "wall3D_6.html");

            // ── 使用自定义 RelayCommand 绑定各命令 ──

            LoadRenderCommand = new RelayCommand(
                execute: _ => ExecuteLoadRender(),
                canExecute: _ => !IsLoading
            );

            ResetViewCommand = new RelayCommand(
                execute: _ => ExecuteResetView(),
                canExecute: _ => !IsLoading
            );

            ToggleBrickLayerCommand = new RelayCommand(
                execute: _ => ExecuteToggleBrickLayer(),
                canExecute: _ => !IsLoading
            );

            ToggleAnnotationCommand = new RelayCommand(
                execute: _ => ExecuteToggleAnnotation(),
                canExecute: _ => !IsLoading
            );

            ExportCommand = new RelayCommand(
                execute: _ => ExecuteExport(),
                canExecute: _ => !IsLoading
            );
        }

        // ────────────────────────────────────────────────
        // 命令执行方法
        // ────────────────────────────────────────────────

        /// <summary>加载 Three.js 墙体渲染页面</summary>
        private void ExecuteLoadRender()
        {
            if (!File.Exists(HtmlFilePath))
            {
                StatusMessage = $"❌ 找不到文件: {HtmlFilePath}";
                MessageBox.Show(
                    $"找不到 wall3D_6.html 文件！\n路径：{HtmlFilePath}",
                    "文件缺失",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            StatusMessage = "🔄 正在加载墙体3D模型渲染...";

            // 触发 View 中注入的导航委托
            NavigateToHtml?.Invoke();
        }

        /// <summary>重置 Three.js 摄像机视角（双击复位等效）</summary>
        private async void ExecuteResetView()
        {
            StatusMessage = "🔄 正在重置视角...";
            if (ExecuteScriptAsync != null)
            {
                // 调用 HTML 页面内的 resetCamera / 双击事件
                await ExecuteScriptAsync(
                    "if(typeof resetCamera === 'function') resetCamera(); " +
                    "else document.dispatchEvent(new MouseEvent('dblclick'));");
                StatusMessage = "✅ 视角已重置";
            }
            else
            {
                StatusMessage = "⚠️ 页面尚未加载，请先点击\"加载渲染\"";
            }
        }

        /// <summary>切换砖块图层可见性</summary>
        private async void ExecuteToggleBrickLayer()
        {
            StatusMessage = "🔄 切换砖块图层...";
            if (ExecuteScriptAsync != null)
            {
                await ExecuteScriptAsync(
                    @"(function(){
                        var cb = document.getElementById('layer-brick');
                        if(cb){ cb.checked = !cb.checked; cb.dispatchEvent(new Event('change')); }
                      })();");
                StatusMessage = "✅ 砖块图层已切换";
            }
            else
            {
                StatusMessage = "⚠️ 页面尚未加载，请先点击\"加载渲染\"";
            }
        }

        /// <summary>切换尺寸标注图层可见性</summary>
        private async void ExecuteToggleAnnotation()
        {
            StatusMessage = "🔄 切换标注图层...";
            if (ExecuteScriptAsync != null)
            {
                await ExecuteScriptAsync(
                    @"(function(){
                        var cb = document.getElementById('layer-dim');
                        if(cb){ cb.checked = !cb.checked; cb.dispatchEvent(new Event('change')); }
                      })();");
                StatusMessage = "✅ 标注图层已切换";
            }
            else
            {
                StatusMessage = "⚠️ 页面尚未加载，请先点击\"加载渲染\"";
            }
        }

        /// <summary>导出提示</summary>
        private void ExecuteExport()
        {
            MessageBox.Show(
                "可在墙体3D模型中使用 renderer.domElement.toDataURL() 导出 PNG 截图。\n" +
                "当前版本为演示，完整导出功能可在此扩展。",
                "导出提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            StatusMessage = "ℹ️ 导出功能已触发（演示）";
        }

        // ────────────────────────────────────────────────
        // 加载完成回调（由 View 在 NavigationCompleted 时调用）
        // ────────────────────────────────────────────────

        public void OnRenderLoaded()
        {
            IsLoading = false;
            StatusMessage = "✅AAC墙体3D渲染模型加载完成";
        }

        public void OnRenderFailed(string error)
        {
            IsLoading = false;
            StatusMessage = $"❌ 渲染加载失败: {error}";
        }
    }
}
