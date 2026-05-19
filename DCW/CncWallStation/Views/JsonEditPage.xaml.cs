using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CncWallStation.Models;
using CncWallStation.ViewModels;

namespace CncWallStation.Views;

/// <summary>
/// JSON 编辑器页面 Code-behind
/// </summary>
public partial class JsonEditPage : Page
{
    private JsonEditPageViewModel ViewModel => (JsonEditPageViewModel)DataContext;

    public JsonEditPage(JsonEditPageViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        KeyDown += JsonEditPage_KeyDown;
    }

    private void JsonEditPage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 && ViewModel.SelectedNode != null)
        {
            StartEditNode(ViewModel.SelectedNode);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && ViewModel.SelectedNode?.Parent != null)
        {
            if (ViewModel.DeleteNodeCommand.CanExecute(null))
                ViewModel.DeleteNodeCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ==================== 编辑框事件 ====================

    /// <summary>
    /// 编辑框加载时自动聚焦并全选内容
    /// </summary>
    private void EditTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    /// <summary>
    /// 编辑框中按键处理：Enter 确认，Escape 取消
    /// </summary>
    private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.ConfirmEditCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ==================== JSON 文本框校验 ====================

    /// <summary>
    /// 文本框失焦时自动校验 JSON 格式
    /// </summary>
    private void JsonTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ViewModel.ValidateTextJson();
    }

    // ==================== TreeView 事件 ====================

    private void JsonTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is JsonTreeNode node)
        {
            ViewModel.SelectedNode = node;
        }
    }

    /// <summary>
    /// 右击前先选中右键的项，确保上下文菜单操作的是正确的节点
    /// </summary>
    private void JsonTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView treeView)
        {
            var hit = e.OriginalSource as DependencyObject;
            while (hit != null && hit != treeView)
            {
                if (hit is TreeViewItem tvi && tvi.DataContext is JsonTreeNode node)
                {
                    tvi.IsSelected = true;
                    node.IsSelected = true;
                    ViewModel.SelectedNode = node;
                    e.Handled = true;
                    return;
                }
                hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
            }
        }
    }

    /// <summary>
    /// 双击树节点值 → 进入编辑模式
    /// </summary>
    private void JsonTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView treeView)
        {
            var hit = e.OriginalSource as DependencyObject;
            while (hit != null && hit != treeView)
            {
                if (hit is TreeViewItem tvi && tvi.DataContext is JsonTreeNode node)
                {
                    StartEditNode(node);
                    e.Handled = true;
                    return;
                }
                hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
            }
        }
    }

    // ==================== 右键菜单事件 ====================

    private void MenuItem_EditValue_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedNode != null)
            StartEditNode(ViewModel.SelectedNode);
    }

    private void MenuItem_ExpandAll_Click(object sender, RoutedEventArgs e)
    {
        SetAllNodesExpanded(ViewModel.TreeNodes, true);
    }

    private void MenuItem_CollapseAll_Click(object sender, RoutedEventArgs e)
    {
        SetAllNodesExpanded(ViewModel.TreeNodes, false);
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 开始编辑节点值（支持 Value 和 Property 类型节点）
    /// </summary>
    private void StartEditNode(JsonTreeNode node)
    {
        if (node.NodeType is JsonNodeType.Value or JsonNodeType.Property)
        {
            node.EditValue = node.Value ?? string.Empty;
            node.IsEditing = true;
        }
    }

    /// <summary>
    /// 递归设置所有节点的展开状态
    /// </summary>
    private static void SetAllNodesExpanded(System.Collections.ObjectModel.ObservableCollection<JsonTreeNode> nodes, bool isExpanded)
    {
        foreach (var node in nodes)
        {
            if (node.Children != null && node.Children.Count > 0)
            {
                node.IsExpanded = isExpanded;
                SetAllNodesExpanded(node.Children, isExpanded);
            }
        }
    }
}
