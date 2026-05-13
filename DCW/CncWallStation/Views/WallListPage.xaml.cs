using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
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

		/// <summary>DataGrid 选择变更 → 同步到 ViewModel</summary>
		private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is DataGrid dg)
			{
				var selected = dg.SelectedItems.Cast<WallListItem>().ToList();
				_viewModel.SelectedItems = new System.Collections.ObjectModel.ObservableCollection<WallListItem>(selected);
			}
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
