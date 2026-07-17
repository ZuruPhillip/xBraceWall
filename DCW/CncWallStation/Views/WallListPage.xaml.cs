using CncWallStation.Localization;
using CncWallStation.Models;
using CncWallStation.Models.Enums;
using CncWallStation.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace CncWallStation.Views
{
    /// <summary>
    /// WallListPage.xaml 的交互逻辑
    /// </summary>
    public partial class WallListPage : Page
    {
        private readonly WallListPageViewModel _viewModel;

        public WallListPage(WallListPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        #region 事件处理

        /// <summary>DataGrid 行高亮变更（仅视觉，不联动 CheckBox）</summary>
        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // DataGrid 行选中仅用于行高亮视觉效果，不以 IsSelected / SelectedItems 为数据源
        }

        /// <summary>行 CheckBox 勾选 → 同步 SelectedItems</summary>
        private void RowCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _viewModel.SyncSelectedItemsAndAllSelected();
        }

        /// <summary>行 CheckBox 取消勾选 → 同步 SelectedItems</summary>
        private void RowCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _viewModel.SyncSelectedItemsAndAllSelected();
        }

        /// <summary>列头 CheckBox 勾选 → 全选当前页</summary>
        private void HeaderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectAllCurrentPage();
        }

        /// <summary>列头 CheckBox 取消 → 全不选当前页</summary>
        private void HeaderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _viewModel.DeselectAllCurrentPage();
        }

        /// <summary>Ctrl+C → 复制当前单元格文本</summary>
        private void DataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.C
                || System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control
                || sender is not DataGrid dg)
                return;

            var cell = dg.CurrentCell;
            if (cell.Column is null) return;
            var element = cell.Column.GetCellContent(cell.Item);
            if (element is null) return;

            var text = ExtractTextFromCell(element, cell.Column);
            if (!string.IsNullOrEmpty(text))
            {
                System.Windows.Clipboard.SetText(text);
                e.Handled = true;
            }
        }

        /// <summary>右键点击时，把目标单元格设为当前单元格（修复右键复制取不到正确值的问题）</summary>
        private void DataGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not DataGrid dg) return;

            // 命中测试找到点击的 DataGridCell
            var hit = VisualTreeHelper.HitTest(dg, e.GetPosition(dg));
            var cell = FindVisualParent<DataGridCell>(hit?.VisualHit as DependencyObject);
            if (cell == null) return;

            // 把点击的行设为选中行，单元格设为当前单元格
            cell.Focus();
            dg.CurrentCell = new DataGridCellInfo(cell);
            e.Handled = true;
        }

        /// <summary>从 DataGrid 单元格的视觉树中提取文本</summary>
        private static string ExtractTextFromCell(FrameworkElement element, DataGridColumn column)
        {
            var textBlock = FindVisualChild<TextBlock>(element);
            if (textBlock != null)
                return textBlock.Text ?? string.Empty;

            return string.Empty;
        }

        /// <summary>右键菜单 → 复制当前单元格内容</summary>
        private void ContextMenu_CopyCell(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Parent: ContextMenu cm } || cm.PlacementTarget is not DataGrid dg)
                return;

            var cell = dg.CurrentCell;
            if (cell.Column is null) return;
            var element = cell.Column.GetCellContent(cell.Item);
            if (element is null) return;

            var text = ExtractTextFromCell(element, cell.Column);
            if (string.IsNullOrEmpty(text)) return;

            var capturedText = text;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { Clipboard.SetText(capturedText); }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // 剪贴板被其他进程占用，静默忽略
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>右键菜单 → 修改墙体名称</summary>
        private void ContextMenu_RenameWall(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Parent: ContextMenu cm } || cm.PlacementTarget is not DataGrid dg)
                return;

            if (dg.CurrentItem is WallListItem item)
            {
                _viewModel.RenameWallCommand.Execute(item);
            }
        }

        /// <summary>右键菜单 → 查看详情</summary>
        private void ContextMenu_ViewDetail(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Parent: ContextMenu cm } || cm.PlacementTarget is not DataGrid dg)
                return;

            if (dg.CurrentItem is WallListItem item)
            {
                _viewModel.ViewDetailCommand.Execute(item);
            }
        }

        /// <summary>右键菜单 → 查看MOM模型渲染</summary>
        private void ContextMenu_ViewMomRender(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Parent: ContextMenu cm } || cm.PlacementTarget is not DataGrid dg)
                return;

            if (dg.CurrentItem is WallListItem item)
            {
                _viewModel.ViewMomRenderCommand.Execute(item);
            }
        }

        /// <summary>右键菜单 → 编辑 JSON</summary>
        private void ContextMenu_EditJson(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Parent: ContextMenu cm } || cm.PlacementTarget is not DataGrid dg)
                return;

            if (dg.CurrentItem is WallListItem item)
            {
                _viewModel.EditJsonDataCommand.Execute(item);
            }
        }

        /// <summary>在视觉树中查找指定类型的子元素</summary>
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>在视觉树中向上查找指定类型的父元素</summary>
        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T found)
                    return found;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        /// <summary>状态 ComboBox 变更 → 自动搜索</summary>
        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.SearchCommand.Execute(null);
        }

        /// <summary>优先级 ComboBox 变更 → 自动搜索</summary>
        private void PriorityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.SearchCommand.Execute(null);
        }

        /// <summary>管线状态 ComboBox 变更 → 自动搜索</summary>
        private void PipelineStageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.SearchCommand.Execute(null);
        }

        /// <summary>审核状态 ComboBox 变更 → 自动搜索</summary>
        private void AuditStatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.SearchCommand.Execute(null);
        }

        /// <summary>版本筛选 ComboBox 变更 → 自动搜索</summary>
        private void IsLatestComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.SearchCommand.Execute(null);
        }

        /// <summary>含已删除 CheckBox 变更 → 自动搜索</summary>
        private void IncludeDeleted_Changed(object sender, RoutedEventArgs e)
        {
            _viewModel.SearchCommand.Execute(null);
        }

        /// <summary>每页条数变更</summary>
        private async void PageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is int size)
            {
                _viewModel.PageSize = size;
                await _viewModel.ApplyFiltersAsync();
            }
        }

        #endregion

        #region 值转换器

        /// <summary>截断 mjson 显示</summary>
        public static readonly IValueConverter TruncateConverter = new TruncateJsonConverter();

        /// <summary>上一页</summary>
        public static readonly IValueConverter PrevPageConverter = new PrevNextPageConverter(true);

        /// <summary>下一页</summary>
        public static readonly IValueConverter NextPageConverter = new PrevNextPageConverter(false);

        #endregion
    }

    #region Converter 实现

    /// <summary>优先级 → 画刷（根据 int 值范围着色）</summary>
    public class PriorityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int p)
            {
                if (p >= 2) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")); // 高
                if (p >= 1) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")); // 中
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")); // 低
            }
            return value is ProcessPriority ep ? ep switch
            {
                ProcessPriority.高 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                ProcessPriority.中 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
                ProcessPriority.低 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                _ => new SolidColorBrush(Colors.Gray)
            } : new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>int 优先级 → 中/英文显示</summary>
    public class PriorityIntToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEn = LocalizationService.Instance.CurrentLanguage.StartsWith("en");
            if (value is int p)
            {
                if (p >= 2) return isEn ? "High" : "高";
                if (p >= 1) return isEn ? "Medium" : "中";
                return isEn ? "Low" : "低";
            }
            return value?.ToString() ?? "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>优先级枚举 → 中/英文</summary>
    public class PriorityToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEn = LocalizationService.Instance.CurrentLanguage.StartsWith("en");
            return value is ProcessPriority p ? (isEn ? p.ToDisplayTextEn() : p.ToDisplayText()) : "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>状态 → 画刷</summary>
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is ProcessStatus s ? s switch
            {
                ProcessStatus.待校验 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E67E22")),
                ProcessStatus.待加工 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                ProcessStatus.加工中 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                ProcessStatus.异常 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                ProcessStatus.暂停 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD591")),
                ProcessStatus.中止 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA39E")),
                ProcessStatus.待质检 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD591")),
                ProcessStatus.已质检 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1890FF")),
                ProcessStatus.已完成 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                _ => new SolidColorBrush(Colors.Gray)
            } : new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>状态 → 中/英文</summary>
    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEn = LocalizationService.Instance.CurrentLanguage.StartsWith("en");
            return value is ProcessStatus s ? (isEn ? s.ToDisplayTextEn() : s.ToDisplayText()) : "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>管线阶段 → 画刷</summary>
    public class PipelineStageToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is PipelineStage s ? s switch
            {
                PipelineStage.Imported => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                PipelineStage.ValidatingBim => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD")),
                PipelineStage.BimValid => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2980B9")),
                PipelineStage.BimInvalid => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                PipelineStage.Converting => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
                PipelineStage.ConversionFailed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                PipelineStage.Converted => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                PipelineStage.ValidatingMom => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD")),
                PipelineStage.MomValid => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2980B9")),
                PipelineStage.MomInvalid => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                PipelineStage.Ready => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A085")),
                _ => new SolidColorBrush(Colors.Gray)
            } : new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>管线阶段 → 中/英文</summary>
    public class PipelineStageToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PipelineStage s)
            {
                bool isEn = LocalizationService.Instance.CurrentLanguage.StartsWith("en");
                return isEn ? s.ToDisplayTextEn() : s.ToDisplayText();
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>审核状态(int) → 画刷</summary>
    public class AuditStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int s ? s switch
            {
                1 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")), // 已审核 - 红色
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"))  // 未审核 - 绿色
            } : new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>优先级 → 描边主色（白底+彩色描边+彩色文字风格）</summary>
    public class PriorityToOutlineBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int p)
            {
                if (p >= 2) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")); // 高
                if (p >= 1) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")); // 中
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1890FF"));              // 低（蓝色）
            }
            return value is ProcessPriority ep ? ep switch
            {
                ProcessPriority.高 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                ProcessPriority.中 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
                ProcessPriority.低 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1890FF")),
                _ => new SolidColorBrush(Colors.Gray)
            } : new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>生产状态 → 浅色背景（淡彩背景+深色文字风格）</summary>
    public class StatusToLightBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is ProcessStatus s ? s switch
            {
                ProcessStatus.待校验 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE7BA")), // 浅橙
                ProcessStatus.待加工 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6F4FF")), // 浅蓝
                ProcessStatus.加工中 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BAE7FF")), // 浅青
                ProcessStatus.异常   => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCCC7")), // 浅红
                ProcessStatus.暂停   => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF7E6")), // 浅黄
                ProcessStatus.中止   => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE7BA")), // 浅橙
                ProcessStatus.待质检 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF7E6")), // 浅黄
                ProcessStatus.已质检 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BAE7FF")), // 浅青
                ProcessStatus.已完成 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9F7BE")), // 浅绿
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
            } : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>生产状态 → 深色文字（淡彩背景+深色文字风格）</summary>
    public class StatusToDarkBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is ProcessStatus s ? s switch
            {
                ProcessStatus.待校验 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AD4E00")), // 深橙
                ProcessStatus.待加工 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0050B3")), // 深蓝
                ProcessStatus.加工中 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0050B3")), // 深蓝
                ProcessStatus.异常   => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A8071A")), // 深红
                ProcessStatus.暂停   => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AD4E00")), // 深橙
                ProcessStatus.中止   => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A8071A")), // 深红
                ProcessStatus.待质检 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AD4E00")), // 深橙
                ProcessStatus.已质检 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0050B3")), // 深蓝
                ProcessStatus.已完成 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#237804")), // 深绿
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8C8C8C"))
            } : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8C8C8C"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>审核状态 → 浅色背景</summary>
    public class AuditStatusToLightBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int s = value is int i ? i : 0;
            return s switch
            {
                1 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9F7BE")), // 已审核 - 浅绿
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF7E6"))  // 未审核 - 浅黄
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>审核状态 → 深色文字</summary>
    public class AuditStatusToDarkBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int s = value is int i ? i : 0;
            return s switch
            {
                1 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#237804")), // 已审核 - 深绿
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AD4E00"))  // 未审核 - 深橙
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>审核状态 → 中/英文</summary>
    public class AuditStatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEn = LocalizationService.Instance.CurrentLanguage.StartsWith("en");
            return value is AuditStatus s
                ? (isEn ? s.ToDisplayTextEn() : s.ToDisplayText())
                : value is int i
                    ? (isEn ? AuditStatusExtensions.FromInt(i).ToDisplayTextEn() : AuditStatusExtensions.FromInt(i).ToDisplayText())
                    : "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>是否最新版本 → 中/英文</summary>
    public class IsLatestToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEn = LocalizationService.Instance.CurrentLanguage.StartsWith("en");
            return value is bool b ? (b ? (isEn ? "Latest Only" : "仅最新版本") : (isEn ? "All" : "不限")) : "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Bool → Visibility</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolVal = value switch
            {
                bool b => b,
                string s => !string.IsNullOrEmpty(s),
                _ => value != null
            };

            if (parameter is string p && p == "invert")
                boolVal = !boolVal;
            return boolVal ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Bool 取反（用于 IsEnabled 绑定）</summary>
    public class BoolInvertConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>截断 JSON 字符串</summary>
    public class TruncateJsonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var json = value as string;
            if (string.IsNullOrEmpty(json))
                return "{}";
            return json.Length > 80 ? json[..80] + "…" : json;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>上一页/下一页计算</summary>
    public class PrevNextPageConverter : IValueConverter
    {
        private readonly bool _isPrev;

        public PrevNextPageConverter(bool isPrev)
        {
            _isPrev = isPrev;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var page = value is int p ? p : 1;
            return _isPrev ? Math.Max(1, page - 1) : page + 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    #endregion
}
