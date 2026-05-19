using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace CncWallStation.Models;

/// <summary>
/// JSON 树节点类型
/// </summary>
public enum JsonNodeType
{
    /// <summary>根对象（Object）</summary>
    Object,
    /// <summary>数组（Array）</summary>
    Array,
    /// <summary>属性（Key-Value 对中的 Key）</summary>
    Property,
    /// <summary>简单值（string/number/boolean/null）</summary>
    Value
}

/// <summary>
/// JSON 树节点模型，支持树形展示与编辑
/// </summary>
public class JsonTreeNode : INotifyPropertyChanged
{
    private string _key = string.Empty;
    private string _displayKey = string.Empty;
    private string? _value;
    private JsonNodeType _nodeType;
    private ObservableCollection<JsonTreeNode>? _children;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isMatched;
    private bool _isEditing;
    private string _editValue = string.Empty;
    private JsonTreeNode? _parent;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>原始 Key 名（英文）</summary>
    public string Key
    {
        get => _key;
        set { _key = value; OnPropertyChanged(); }
    }

    /// <summary>显示用的 Key 名（中文翻译后）</summary>
    public string DisplayKey
    {
        get => _displayKey;
        set { _displayKey = value; OnPropertyChanged(); }
    }

    /// <summary>节点值（字符串形式）</summary>
    public string? Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValuePreview)); }
    }

    /// <summary>值预览（截断显示）</summary>
    public string ValuePreview =>
        string.IsNullOrEmpty(_value) ? "" :
        _value.Length > 50 ? _value[..50] + "..." : _value;

    /// <summary>节点类型</summary>
    public JsonNodeType NodeType
    {
        get => _nodeType;
        set { _nodeType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsExpandable)); OnPropertyChanged(nameof(IsValueNode)); }
    }

    /// <summary>子节点集合</summary>
    public ObservableCollection<JsonTreeNode>? Children
    {
        get => _children;
        set { _children = value; OnPropertyChanged(); }
    }

    /// <summary>是否已展开</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    /// <summary>是否被选中</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>搜索是否匹配</summary>
    public bool IsMatched
    {
        get => _isMatched;
        set { _isMatched = value; OnPropertyChanged(); }
    }

    /// <summary>是否处于编辑状态</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    /// <summary>编辑中的值</summary>
    public string EditValue
    {
        get => _editValue;
        set { _editValue = value; OnPropertyChanged(); }
    }

    /// <summary>父节点</summary>
    public JsonTreeNode? Parent
    {
        get => _parent;
        set { _parent = value; OnPropertyChanged(); }
    }

    /// <summary>JToken 引用（用于直接修改 JSON）</summary>
    public JToken? Token { get; set; }

    /// <summary>是否有子节点可展开</summary>
    public bool IsExpandable =>
        NodeType is JsonNodeType.Object or JsonNodeType.Array ||
        (Children is not null && Children.Count > 0);

    /// <summary>是否是值节点（可编辑值）</summary>
    public bool IsValueNode => NodeType == JsonNodeType.Value;

    /// <summary>子项数量描述</summary>
    public string ChildCountDescription
    {
        get
        {
            if (Children is null || Children.Count == 0)
                return NodeType switch
                {
                    JsonNodeType.Object => "{ }",
                    JsonNodeType.Array => "[ ]",
                    _ => ""
                };

            return NodeType switch
            {
                JsonNodeType.Object => $"{{ {Children.Count} 个属性 }}",
                JsonNodeType.Array => $"[ {Children.Count} 项 ]",
                _ => ""
            };
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
