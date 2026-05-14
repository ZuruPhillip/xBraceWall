using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CncWallStation.Models;
using CncWallStation.ViewModels;

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
			var element = cell.Column.GetCellContent(cell.Item);
			if (element is null) return;

			var text = ExtractTextFromCell(element, cell.Column);
			if (!string.IsNullOrEmpty(text))
			{
				System.Windows.Clipboard.SetText(text);
				e.Handled = true;
			}
		}

		/// <summary>从 DataGrid 单元格的视觉树中提取文本</summary>
		private static string ExtractTextFromCell(FrameworkElement element, DataGridColumn column)
		{
			// 遍历视觉树找 TextBlock
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
			var element = cell.Column.GetCellContent(cell.Item);
			if (element is null) return;

			var text = ExtractTextFromCell(element, cell.Column);
			if (string.IsNullOrEmpty(text)) return;

			// ContextMenu 打开期间剪贴板被 WPF 锁定，延迟到菜单关闭后再写入
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

		private static string PriorityToText(ProcessPriority p) => p switch
		{
			ProcessPriority.高 => "高",
			ProcessPriority.中 => "中",
			ProcessPriority.低 => "低",
			_ => "未知"
		};

		private static string StatusToText(ProcessStatus s) => s switch
		{
			ProcessStatus.待加工 => "待加工",
			ProcessStatus.加工中 => "加工中",
			ProcessStatus.已完成 => "已完成",
			ProcessStatus.异常 => "异常",
			_ => "未知"
		};

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

		/// <summary>状态多选 ListBox 变更</summary>
		private void StatusListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is ListBox lb)
			{
				_viewModel.SelectedStatuses = new System.Collections.ObjectModel.ObservableCollection<ProcessStatus>(
					lb.SelectedItems.Cast<ProcessStatus>());
				_viewModel.SearchCommand.Execute(null);
			}
		}

		/// <summary>优先级多选 ListBox 变更</summary>
		private void PriorityListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is ListBox lb)
			{
				_viewModel.SelectedPriorities = new System.Collections.ObjectModel.ObservableCollection<ProcessPriority>(
					lb.SelectedItems.Cast<ProcessPriority>());
				_viewModel.SearchCommand.Execute(null);
			}
		}

		/// <summary>每页条数变更</summary>
		private void PageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is ComboBox cb && cb.SelectedItem is int size)
			{
				_viewModel.PageSize = size;
				_viewModel.ApplyFilters();
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

	/// <summary>优先级 → 画刷</summary>
	public class PriorityToBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is ProcessPriority p ? p switch
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

	/// <summary>优先级 → 中文</summary>
	public class PriorityToTextConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is ProcessPriority p ? p switch
			{
				ProcessPriority.高 => "高",
				ProcessPriority.中 => "中",
				ProcessPriority.低 => "低",
				_ => "未知"
			} : "未知";
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
				ProcessStatus.待加工 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
				ProcessStatus.加工中 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
				ProcessStatus.已完成 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
				ProcessStatus.异常 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
				_ => new SolidColorBrush(Colors.Gray)
			} : new SolidColorBrush(Colors.Gray);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}

	/// <summary>状态 → 中文</summary>
	public class StatusToTextConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is ProcessStatus s ? s switch
			{
				ProcessStatus.待加工 => "待加工",
				ProcessStatus.加工中 => "加工中",
				ProcessStatus.已完成 => "已完成",
				ProcessStatus.异常 => "异常",
				_ => "未知"
			} : "未知";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}

	/// <summary>Bool → Visibility</summary>
	public class BoolToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var boolVal = value is bool b && b;
			if (parameter is string s && s == "invert")
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
