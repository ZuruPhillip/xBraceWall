using CncWallStation.Models;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CncWallStation.Services.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RelayCommand = CncWallStation.Commands.RelayCommand;

namespace CncWallStation.ViewModels;

/// <summary>
/// JSON 编辑器页面 ViewModel
/// 支持树视图展开、节点增删改查、JSON校验与异常定位、Key中文翻译
/// 保存时将 PipelineStage 设为 待校验(Imported=0)
/// </summary>
public partial class JsonEditPageViewModel : ObservableObject
{
    private readonly IWallAppService _wallAppService;
    private readonly JsonKeyTranslationConfig _translationConfig;

    // ==================== 属性 ====================

    /// <summary>当前墙体 ID</summary>
    [ObservableProperty]
    private string _wallId;

    /// <summary>当前墙体详情</summary>
    private WallDetailDto? _wallDetail;

    /// <summary>原始 JSON 文本（左侧编辑器）</summary>
    [ObservableProperty]
    private string _jsonText = string.Empty;

    /// <summary>格式化后的 JSON 文本</summary>
    [ObservableProperty]
    private string _formattedJsonText = string.Empty;

    /// <summary>树节点集合</summary>
    [ObservableProperty]
    private ObservableCollection<JsonTreeNode> _treeNodes = new();

    /// <summary>当前选中的树节点</summary>
    [ObservableProperty]
    private JsonTreeNode? _selectedNode;

    /// <summary>搜索文本</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>状态消息</summary>
    [ObservableProperty]
    private string _statusMessage = "就绪";

    /// <summary>状态消息颜色（红色表示错误，绿色表示成功）</summary>
    [ObservableProperty]
    private string _statusColor = "#888888";

    /// <summary>是否正在加载</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>是否有未保存的修改</summary>
    [ObservableProperty]
    private bool _hasChanges;

    /// <summary>JSON 错误信息</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>错误行号</summary>
    [ObservableProperty]
    private int _errorLine;

    /// <summary>错误列号</summary>
    [ObservableProperty]
    private int _errorColumn;

    /// <summary>是否有错误</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>搜索匹配数量</summary>
    [ObservableProperty]
    private int _matchCount;

    /// <summary>JToken 根对象（用于读写 JSON）</summary>
    private JToken? _rootToken;

    // ==================== 命令 ====================

    /// <summary>加载墙体数据命令</summary>
    public ICommand LoadCommand { get; }

    /// <summary>保存命令</summary>
    public ICommand SaveCommand { get; }

    /// <summary>搜索节点命令</summary>
    public ICommand SearchCommand { get; }

    /// <summary>清除搜索命令</summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>删除节点命令</summary>
    public ICommand DeleteNodeCommand { get; }

    /// <summary>格式化 JSON 命令</summary>
    public ICommand FormatJsonCommand { get; }

    /// <summary>从文本刷新树命令</summary>
    public ICommand RefreshTreeCommand { get; }

    /// <summary>确认编辑节点值命令</summary>
    public ICommand ConfirmEditCommand { get; }

    /// <summary>取消编辑命令</summary>
    public ICommand CancelEditCommand { get; }

    // ==================== 构造函数 ====================

    public JsonEditPageViewModel(IWallAppService wallAppService, JsonKeyTranslationConfig translationConfig)
    {
        _wallAppService = wallAppService;
        _translationConfig = translationConfig;

        LoadCommand = new RelayCommand(async _ => await LoadAsync());
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => !IsLoading);
        SearchCommand = new RelayCommand(_ => SearchNodes());
        ClearSearchCommand = new RelayCommand(_ => ClearSearch());
        DeleteNodeCommand = new RelayCommand(_ => DeleteNode(), _ => CanDeleteNode());
        FormatJsonCommand = new RelayCommand(_ => FormatJson());
        RefreshTreeCommand = new RelayCommand(_ => RefreshTreeFromText());
        ConfirmEditCommand = new RelayCommand(_ => ConfirmEdit());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
    }

    // ==================== 公共方法 ====================

    /// <summary>
    /// 设置墙体 ID 并加载数据
    /// </summary>
    public async Task SetWallIdAsync(string wallId)
    {
        WallId = wallId;
        await LoadAsync();
    }

    /// <summary>
    /// 加载墙体 JSON 数据
    /// </summary>
    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(WallId))
        {
            ShowError("墙体 ID 不能为空");
            return;
        }

        IsLoading = true;
        ClearError();
        SetStatus("正在加载...", "#1677FF");

        try
        {
            _wallDetail = await _wallAppService.GetDetailByWallIdAsync(WallId);
            if (_wallDetail == null)
            {
                ShowError($"未找到墙体 '{WallId}'");
                return;
            }

            JsonText = _wallDetail.BimJsonData ?? string.Empty;
            FormattedJsonText = JsonText; // 同时同步编辑区显示

            if (string.IsNullOrWhiteSpace(JsonText))
            {
                SetStatus("BimJson 为空，请导入或手动输入 JSON 数据", "#FF9800");
                TreeNodes.Clear();
                _rootToken = null;
            }
            else
            {
                ParseAndBuildTree(JsonText);
                FormatJson();
                SetStatus($"加载成功 - {_wallDetail.ProjectNumber ?? "未知项目"} / 楼层 {_wallDetail.Floor}", "#4CAF50");
            }

            HasChanges = false;
        }
        catch (Exception ex)
        {
            ShowError($"加载失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 保存 JSON 数据到墙体表，PipelineStage 设为 待校验
    /// </summary>
    public async Task SaveAsync()
    {
        // 0. 将文本编辑区的最新内容同步到 JsonText（文本编辑区绑定 FormattedJsonText）
        JsonText = FormattedJsonText;

        // 1. 空值检查
        if (string.IsNullOrWhiteSpace(JsonText))
        {
            ShowError("BimJson 为空，不允许保存。请先输入或导入 JSON 数据。");
            return;
        }

        // 2. JSON 格式校验（同时获取格式化后的 JSON）
        if (!ValidateJson(JsonText, out var token))
        {
            return;
        }

        // 使用校验后重新格式化的 JSON 文本保存（保证数据库中是一致格式）
        var saveJson = token?.ToString(Formatting.Indented) ?? JsonText;

        // 3. 墙体数据未加载
        if (_wallDetail == null)
        {
            ShowError("墙体数据未加载，请先点击\"加载\"按钮。");
            return;
        }

        IsLoading = true;
        SetStatus("正在保存...", "#1677FF");

        try
        {
            var updatedBy = Environment.UserName;

            // 4. 更新 JSON 数据
            await _wallAppService.UpdateJsonDataAsync(
                wallId: _wallDetail.Id,
                bimJsonData: saveJson,
                momJsonData: null,
                updatedBy: updatedBy
            );

            // 5. 同步将 PipelineStage 设为 待校验
            await _wallAppService.UpdatePipelineStageAsync(
                wallId: _wallDetail.Id,
                stage: PipelineStage.Imported
            );

            // 6. 保存成功后刷新状态
            JsonText = saveJson;
            ParseAndBuildTree(saveJson);
            FormatJson();
            HasChanges = false;

            SetStatus($"保存成功 - {DateTime.Now:yyyy-MM-dd HH:mm:ss} by {updatedBy}", "#4CAF50");
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 格式化 JSON 文本
    /// </summary>
    /// <summary>
    /// 格式化 JSON 文本（基于文本编辑区当前内容，空时回退到 JsonText）
    /// </summary>
    public void FormatJson()
    {
        try
        {
            var source = string.IsNullOrWhiteSpace(FormattedJsonText) ? JsonText : FormattedJsonText;
            if (string.IsNullOrWhiteSpace(source))
            {
                FormattedJsonText = string.Empty;
                return;
            }

            var token = JToken.Parse(source);
            var formatted = token.ToString(Formatting.Indented);
            JsonText = formatted;
            FormattedJsonText = formatted;
        }
        catch
        {
            // 格式化失败不覆盖
        }
    }

    /// <summary>
    /// 实时校验文本编辑区 JSON 格式（仅校验，不修改树）
    /// </summary>
    public void ValidateTextJson()
    {
        if (string.IsNullOrWhiteSpace(FormattedJsonText))
        {
            ClearError();
            return;
        }

        try
        {
            JToken.Parse(FormattedJsonText);
            ClearError();
            SetStatus("JSON 格式正确", "#4CAF50");
        }
        catch (JsonReaderException ex)
        {
            ErrorLine = ex.LineNumber;
            ErrorColumn = ex.LinePosition;
            ErrorMessage = $"JSON 格式错误: {ex.Message}";
            HasError = true;
            SetStatus($"行 {ex.LineNumber}, 列 {ex.LinePosition}: {ex.Message}", "#FF1744");
        }
        catch (Exception ex)
        {
            ShowError($"JSON 解析错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 从文本编辑器刷新树视图
    /// </summary>
    public void RefreshTreeFromText()
    {
        // 从文本编辑区读取最新内容（文本编辑区绑定 FormattedJsonText）
        JsonText = FormattedJsonText;

        if (string.IsNullOrWhiteSpace(JsonText))
        {
            TreeNodes.Clear();
            _rootToken = null;
            return;
        }

        if (!ValidateJson(JsonText, out _))
            return;

        ParseAndBuildTree(JsonText);
        FormatJson();
        HasChanges = true;

        // 重新执行搜索（如果有搜索文本）
        if (!string.IsNullOrWhiteSpace(SearchText))
            SearchNodes();
        else
            SetStatus("树视图已刷新", "#1677FF");
    }

    /// <summary>
    /// 检查当前选中节点的可用操作
    /// </summary>
    public List<string> GetAvailableActions()
    {
        var actions = new List<string>();
        if (SelectedNode == null) return actions;

        if (SelectedNode.Parent != null)
            actions.Add("删除节点");

        if (SelectedNode.NodeType is JsonNodeType.Value or JsonNodeType.Property)
            actions.Add("编辑值");

        return actions;
    }

    // ==================== JSON 校验 ====================

    /// <summary>
    /// 校验 JSON 格式，返回是否有效，并设置错误信息
    /// </summary>
    private bool ValidateJson(string json, out JToken? token)
    {
        token = null;
        ClearError();

        if (string.IsNullOrWhiteSpace(json))
        {
            ShowError("JSON 内容为空");
            return false;
        }

        try
        {
            token = JToken.Parse(json);
            SetStatus("JSON 格式校验通过", "#4CAF50");
            return true;
        }
        catch (JsonReaderException ex)
        {
            ErrorLine = ex.LineNumber;
            ErrorColumn = ex.LinePosition;
            ErrorMessage = $"JSON 格式错误: {ex.Message}";
            HasError = true;
            StatusColor = "#FF1744";
            SetStatus($"行 {ex.LineNumber}, 列 {ex.LinePosition}: {ex.Message}", "#FF1744");
            return false;
        }
        catch (JsonException ex)
        {
            ShowError($"JSON 解析错误: {ex.Message}");
            return false;
        }
    }

    // ==================== 树构建与搜索 ====================

    /// <summary>
    /// 解析 JSON 文本并构建树节点
    /// </summary>
    private void ParseAndBuildTree(string json)
    {
        var token = JToken.Parse(json);
        _rootToken = token;

        TreeNodes = new ObservableCollection<JsonTreeNode>(
            new[] { BuildTreeNodeFromToken(token, "root") }
        );

        // 默认展开根节点
        if (TreeNodes.Count > 0)
        {
            TreeNodes[0].IsExpanded = true;
        }
    }

    /// <summary>
    /// 从 JToken 递归构建树节点
    /// </summary>
    private JsonTreeNode BuildTreeNodeFromToken(JToken token, string key, JsonTreeNode? parent = null)
    {
        if (token is JObject obj)
        {
            var node = new JsonTreeNode
            {
                Key = key,
                DisplayKey = _translationConfig.GetTranslation(key),
                NodeType = JsonNodeType.Object,
                Token = token,
                Parent = parent,
                Children = new ObservableCollection<JsonTreeNode>()
            };

            foreach (var prop in obj.Properties())
            {
                var child = BuildTreeNodeFromToken(prop.Value, prop.Name, node);
                child.Token = prop; // 保留 JProperty 引用以便编辑
                node.Children.Add(child);
            }

            return node;
        }

        if (token is JArray array)
        {
            var node = new JsonTreeNode
            {
                Key = key,
                DisplayKey = _translationConfig.GetTranslation(key),
                NodeType = JsonNodeType.Array,
                Token = token,
                Parent = parent,
                Children = new ObservableCollection<JsonTreeNode>()
            };

            for (int i = 0; i < array.Count; i++)
            {
                var child = BuildTreeNodeFromToken(array[i], $"[{i}]", node);
                node.Children.Add(child);
            }

            return node;
        }

        if (token is JProperty prop2)
        {
            var childNode = BuildTreeNodeFromToken(prop2.Value, prop2.Name, parent);
            childNode.Token = prop2;
            return childNode;
        }

        // JValue
        var valueStr = token switch
        {
            JValue v => v.Value?.ToString(),
            _ => token.ToString(Formatting.None)
        };

        return new JsonTreeNode
        {
            Key = key,
            DisplayKey = _translationConfig.GetTranslation(key),
            Value = valueStr,
            NodeType = JsonNodeType.Value,
            Token = token,
            Parent = parent
        };
    }

    /// <summary>
    /// 搜索节点（Key 或 Value 匹配）
    /// </summary>
    private void SearchNodes()
    {
        // 清除之前的搜索结果
        ClearSearchHighlight(TreeNodes);

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            MatchCount = 0;
            SetStatus("就绪", "#888888");
            return;
        }

        MatchCount = 0;
        var matchedNodes = new List<JsonTreeNode>();

        // 递归搜索
        SearchNodesRecursive(TreeNodes, SearchText, matchedNodes);

        // 展开所有匹配节点的父节点路径
        foreach (var node in matchedNodes)
        {
            ExpandParentPath(node);
            node.IsMatched = true;
        }

        MatchCount = matchedNodes.Count;
        OnPropertyChanged(nameof(MatchCount));

        if (MatchCount > 0)
            SetStatus($"找到 {MatchCount} 个匹配节点", "#4CAF50");
        else
            SetStatus("未找到匹配节点", "#FF9800");
    }

    private void SearchNodesRecursive(IEnumerable<JsonTreeNode> nodes, string searchText, List<JsonTreeNode> results)
    {
        foreach (var node in nodes)
        {
            var keyMatches = node.Key.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            var valueMatches = node.Value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
            var displayKeyMatches = node.DisplayKey.Contains(searchText, StringComparison.OrdinalIgnoreCase);

            if (keyMatches || valueMatches || displayKeyMatches)
                results.Add(node);

            if (node.Children != null)
                SearchNodesRecursive(node.Children, searchText, results);
        }
    }

    private void ExpandParentPath(JsonTreeNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    private void ClearSearchHighlight(IEnumerable<JsonTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsMatched = false;
            if (node.Children != null)
                ClearSearchHighlight(node.Children);
        }
    }

    /// <summary>
    /// 清除搜索
    /// </summary>
    private void ClearSearch()
    {
        SearchText = string.Empty;
        ClearSearchHighlight(TreeNodes);
        MatchCount = 0;
        SetStatus("就绪", "#888888");
    }

    // ==================== 节点编辑 ====================

    /// <summary>
    /// 删除选中节点（支持任意节点，含所有子节点）
    /// </summary>
    private void DeleteNode()
    {
        if (SelectedNode?.Parent == null || SelectedNode.Token == null)
            return;

        var nodeKey = SelectedNode.Key;
        var parentToken = SelectedNode.Parent.Token;

        try
        {
            bool removed = false;

            // 情况1：父节点是 JObject，当前节点是 JProperty（最常见）
            if (parentToken is JObject parentObj && SelectedNode.Token is JProperty prop)
            {
                prop.Remove();
                removed = true;
            }
            // 情况2：父节点直接是 JArray（如根为数组时的直接子项）
            else if (parentToken is JArray parentArr)
            {
                SelectedNode.Token.Remove();
                removed = true;
            }
            // 情况3：父节点是 JProperty 包裹的 JArray（对象属性值为数组的项）
            else if (parentToken is JProperty parentProp && parentProp.Value is JArray parentArr2)
            {
                SelectedNode.Token.Remove();
                removed = true;
            }
            // 情况4：父节点是 JProperty，当前节点是 JToken（直接移除）
            else if (SelectedNode.Token is JToken tok && tok.Parent != null)
            {
                tok.Remove();
                removed = true;
            }

            if (removed)
            {
                RefreshTree(_rootToken);
                SetStatus($"已删除节点 '{nodeKey}'", "#4CAF50");
                HasChanges = true;
            }
            else
            {
                ShowError("无法删除此节点：节点结构不匹配");
            }
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 确认编辑节点值
    /// </summary>
    private void ConfirmEdit()
    {
        if (SelectedNode == null || SelectedNode.Token == null)
            return;

        try
        {
            if (SelectedNode.NodeType == JsonNodeType.Value && SelectedNode.Token is JValue jv)
            {
                // 尝试智能类型转换
                jv.Value = ParseValue(SelectedNode.EditValue);
            }
            else if (SelectedNode.Token is JProperty prop)
            {
                var parsedValue = ParseValue(SelectedNode.EditValue);
                prop.Value = parsedValue is null
                    ? JValue.CreateNull()
                    : new JValue(parsedValue);
            }

            RefreshTree(_rootToken);
            HasChanges = true;
            SelectedNode.IsEditing = false;
            SetStatus("节点已更新", "#4CAF50");
        }
        catch (Exception ex)
        {
            ShowError($"编辑失败: {ex.Message}");
            SelectedNode.IsEditing = false;
        }
    }

    /// <summary>
    /// 取消编辑
    /// </summary>
    private void CancelEdit()
    {
        if (SelectedNode != null)
        {
            SelectedNode.IsEditing = false;
            SelectedNode.EditValue = string.Empty;
        }
    }

    /// <summary>
    /// 解析值（智能类型推断）
    /// </summary>
    private static object? ParseValue(string input)
    {
        if (string.IsNullOrEmpty(input) || input == "null")
            return null;

        if (input == "true")
            return true;

        if (input == "false")
            return false;

        if (long.TryParse(input, out var l))
            return l;

        if (double.TryParse(input, out var d))
            return d;

        return input;
    }

    /// <summary>
    /// 获取 Object 中唯一的键名
    /// 刷新树视图（保留展开状态）
    /// </summary>
    private void RefreshTree(JToken? token)
    {
        if (token == null) return;

        // 记录当前展开状态
        var expandedKeys = new HashSet<string>();
        CollectExpandedNodes(TreeNodes, expandedKeys, "");

        _rootToken = token;
        JsonText = token.ToString(Formatting.Indented);
        FormattedJsonText = JsonText; // 同步编辑区显示
        FormatJson();

        TreeNodes = new ObservableCollection<JsonTreeNode>(
            new[] { BuildTreeNodeFromToken(token, "root") }
        );

        // 恢复展开状态
        RestoreExpandedNodes(TreeNodes, expandedKeys, "");
        OnPropertyChanged(nameof(TreeNodes));
    }

    private void CollectExpandedNodes(IEnumerable<JsonTreeNode> nodes, HashSet<string> keys, string path)
    {
        foreach (var node in nodes)
        {
            var currentPath = string.IsNullOrEmpty(path) ? node.Key : $"{path}.{node.Key}";
            if (node.IsExpanded)
                keys.Add(currentPath);

            if (node.Children != null)
                CollectExpandedNodes(node.Children, keys, currentPath);
        }
    }

    private void RestoreExpandedNodes(IEnumerable<JsonTreeNode> nodes, HashSet<string> keys, string path)
    {
        foreach (var node in nodes)
        {
            var currentPath = string.IsNullOrEmpty(path) ? node.Key : $"{path}.{node.Key}";
            if (keys.Contains(currentPath))
                node.IsExpanded = true;

            if (node.Children != null)
                RestoreExpandedNodes(node.Children, keys, currentPath);
        }
    }

    // ==================== 权限校验 ====================

    private bool CanDeleteNode()
    {
        return SelectedNode != null && SelectedNode.Parent != null && _rootToken != null;
    }

    // ==================== 状态辅助方法 ====================

    private void SetStatus(string message, string color)
    {
        StatusMessage = message;
        StatusColor = color;
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        StatusMessage = message;
        StatusColor = "#FF1744";
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        ErrorLine = 0;
        ErrorColumn = 0;
    }
}
