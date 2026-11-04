using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Localization;
using CncWallStation.Models.Dtos;
using CncWallStation.Services.Application;
using CncWallStation.Services.OpcUa;
using CncWallStation.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace CncWallStation.ViewModels
{
    /// <summary>
    /// 【OPC节点配置】页面 ViewModel：
    /// 分组管理 + 节点查询/分页 + 节点增删改（弹窗）+ Excel 导入导出 + 实时值订阅
    /// </summary>
    public partial class OpcNodeConfigViewModel : ObservableObject, IDisposable
    {
        private const int DefaultPageSize = 20;

        /// <summary>导入预览中最多展示的错误条数（避免弹窗过长）</summary>
        private const int MaxDisplayedImportErrors = 20;

        /// <summary>旧版本地节点配置路径（仅用于一次性迁移）</summary>
        private static readonly string LegacyNodesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CncWallStation", "opc_nodes.json");

        private readonly IOpcNodeAppService _nodeAppService;
        private readonly IOpcNodeExcelService _excelService;
        private readonly IOpcUaService _opcUaService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OpcNodeConfigViewModel> _logger;
        private readonly DispatcherTimer _keywordDebounceTimer;

        /// <summary>按 NodeId 缓存节点实例，避免刷新时重建对象导致实时值停止更新（与 OpcUaService 的订阅键一致）</summary>
        private readonly Dictionary<string, OpcNodeConfig> _nodeInstanceMap = new(StringComparer.OrdinalIgnoreCase);

        private List<OpcNodeConfig> _allNodes = new();
        private List<OpcNodeConfig> _filteredNodes = new();

        private bool _suppressFilter;
        private bool _isLoading;
        private bool _isReloading;
        private bool _disposed;

        public OpcNodeConfigViewModel(
            IOpcNodeAppService nodeAppService,
            IOpcNodeExcelService excelService,
            IOpcUaService opcUaService,
            IServiceProvider serviceProvider,
            ILogger<OpcNodeConfigViewModel> logger)
        {
            _nodeAppService = nodeAppService;
            _excelService = excelService;
            _opcUaService = opcUaService;
            _serviceProvider = serviceProvider;
            _logger = logger;

            _keywordDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _keywordDebounceTimer.Tick += OnKeywordDebounceTick;

            Groups.Add(new OpcNodeGroupItem(null, true, LocalizationService.Instance["OpcNode_GroupAll"]));
            SelectedGroup = Groups[0];
        }

        // ══════════════════════════════════════════
        //  集合
        // ══════════════════════════════════════════

        /// <summary>左侧分组栏（含“全部节点”“未分组”伪条目）</summary>
        public ObservableCollection<OpcNodeGroupItem> Groups { get; } = new();

        /// <summary>当前页节点（表格数据源）</summary>
        public ObservableCollection<OpcNodeConfig> PageNodes { get; } = new();

        /// <summary>分页条页码按钮</summary>
        public ObservableCollection<PageNumberItem> PageNumbers { get; } = new();

        /// <summary>每页条数可选项</summary>
        public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 20, 50, 100 };

        /// <summary>可写状态筛选选项下标：0=全部 1=可写 2=只读</summary>
        public IReadOnlyList<string> WritableFilterOptions { get; } = new[]
        {
            LocalizationService.Instance["OpcNode_FilterAll"],
            LocalizationService.Instance["OpcNode_FilterWritable"],
            LocalizationService.Instance["OpcNode_FilterReadOnly"]
        };

        /// <summary>实时显示筛选选项下标：0=全部 1=显示 2=不显示</summary>
        public IReadOnlyList<string> RealtimeFilterOptions { get; } = new[]
        {
            LocalizationService.Instance["OpcNode_FilterAll"],
            LocalizationService.Instance["OpcNode_FilterRealtimeYes"],
            LocalizationService.Instance["OpcNode_FilterRealtimeNo"]
        };

        // ══════════════════════════════════════════
        //  查询条件
        // ══════════════════════════════════════════

        [ObservableProperty]
        private OpcNodeGroupItem? _selectedGroup;

        [ObservableProperty]
        private string _keyword = string.Empty;

        [ObservableProperty]
        private int _writableFilterIndex;

        [ObservableProperty]
        private int _realtimeFilterIndex;

        [ObservableProperty]
        private bool _hasActiveFilter;

        [ObservableProperty]
        private bool _hasKeywordChip;

        [ObservableProperty]
        private string _keywordChipText = string.Empty;

        [ObservableProperty]
        private bool _hasGroupChip;

        [ObservableProperty]
        private string _groupChipText = string.Empty;

        [ObservableProperty]
        private bool _hasWritableChip;

        [ObservableProperty]
        private string _writableChipText = string.Empty;

        [ObservableProperty]
        private bool _hasRealtimeChip;

        [ObservableProperty]
        private string _realtimeChipText = string.Empty;

        // ══════════════════════════════════════════
        //  分页 / 状态
        // ══════════════════════════════════════════

        [ObservableProperty]
        private int _pageIndex = 1;

        [ObservableProperty]
        private int _pageSize = DefaultPageSize;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private bool _canGoPrevious;

        [ObservableProperty]
        private bool _canGoNext;

        [ObservableProperty]
        private bool _hasNoResult;

        [ObservableProperty]
        private bool _isBusy;

        /// <summary>底部统计文本，如 “共 128 条 · 当前 1-20 条”</summary>
        [ObservableProperty]
        private string _pageInfoText = string.Empty;

        // ══════════════════════════════════════════
        //  初始化 / 加载
        // ══════════════════════════════════════════

        /// <summary>
        /// 页面加载入口（页面 Loaded 时调用）。
        /// 首次加载会把旧版 opc_nodes.json 迁移进数据库，随后从数据库刷新列表。
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isLoading)
                return;

            try
            {
                _isLoading = true;

                var migrated = await _nodeAppService.MigrateLegacyJsonIfEmptyAsync(LegacyNodesFilePath);
                if (migrated > 0)
                {
                    _logger.LogInformation("旧版 OPC 节点配置已迁移至数据库: {Count} 个", migrated);
                }

                await ReloadAsync();
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>页签重新激活时刷新（保持当前查询条件与分页位置）</summary>
        public async Task OnActivatedAsync()
        {
            if (_isLoading)
                return;

            await ReloadAsync();
        }

        /// <summary>重新从数据库加载分组与节点，并保持当前查询条件</summary>
        private async Task ReloadAsync()
        {
            // 防止页签快速来回切换导致并发刷新
            if (_isReloading)
                return;

            _isReloading = true;
            IsBusy = true;
            try
            {
                var previousGroup = SelectedGroup;

                // 1. 节点（全量，查询在内存中完成，保证翻页/查询无抖动）
                var nodeDtos = await _nodeAppService.GetNodesAsync();
                SyncNodeInstances(nodeDtos);

                // 2. 分组（计数基于最新节点集合）
                var groupDtos = await _nodeAppService.GetGroupsAsync();
                RebuildGroups(groupDtos, previousGroup);

                // 3. 应用查询条件
                ApplyFilter();

                // 4. 订阅节点以获取实时值（离线时 OpcUaService 会暂存，连接后自动恢复）
                await SubscribeNodesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载 OPC 节点配置失败");
                ShowError(string.Format(LocalizationService.Instance["OpcNode_LoadFailed"], ex.Message));
            }
            finally
            {
                IsBusy = false;
                _isReloading = false;
            }
        }

        /// <summary>
        /// 把数据库 DTO 同步到内存节点集合。
        /// 已存在的节点复用原实例（OpcUaService 按 NodeId 去重，换新对象会导致实时值停止刷新）。
        /// 缓存按 NodeId 建立且不主动清理：OpcUaService 自身长期持有已订阅节点的引用，
        /// 这里保留实例可以让“删除后重新添加同一 NodeId”的节点继续收到实时值。
        /// </summary>
        private void SyncNodeInstances(IReadOnlyList<OpcNodeDto> dtos)
        {
            var result = new List<OpcNodeConfig>(dtos.Count);

            foreach (var dto in dtos)
            {
                if (_nodeInstanceMap.TryGetValue(dto.NodeId, out var node))
                {
                    node.Id = dto.Id;
                    node.Description = dto.Description;
                    node.IsWritable = dto.IsWritable;
                    node.IsShowInRealtime = dto.IsShowInRealtime;
                    node.GroupId = dto.GroupId;
                    node.GroupName = dto.GroupName;
                }
                else
                {
                    node = new OpcNodeConfig
                    {
                        Id = dto.Id,
                        NodeId = dto.NodeId,
                        Description = dto.Description,
                        IsWritable = dto.IsWritable,
                        IsShowInRealtime = dto.IsShowInRealtime,
                        GroupId = dto.GroupId,
                        GroupName = dto.GroupName
                    };
                    _nodeInstanceMap[dto.NodeId] = node;
                }

                result.Add(node);
            }

            _allNodes = result;
        }

        /// <summary>重建左侧分组栏，并尽量保持原选中项</summary>
        private void RebuildGroups(IReadOnlyList<OpcNodeGroupDto> groupDtos, OpcNodeGroupItem? previousGroup)
        {
            var previousIsAll = previousGroup?.IsAll ?? true;
            var previousId = previousGroup?.GroupId;

            _suppressFilter = true;
            try
            {
                Groups.Clear();

                Groups.Add(new OpcNodeGroupItem(
                    null,
                    true,
                    LocalizationService.Instance["OpcNode_GroupAll"],
                    _allNodes.Count));

                Groups.Add(new OpcNodeGroupItem(
                    null,
                    false,
                    LocalizationService.Instance["OpcNode_Ungrouped"],
                    _allNodes.Count(n => n.GroupId == null)));

                foreach (var dto in groupDtos)
                    Groups.Add(new OpcNodeGroupItem(dto.Id, false, dto.Name, dto.NodeCount));

                var target = previousGroup == null || previousIsAll
                    ? Groups[0]
                    : Groups.FirstOrDefault(g => g.GroupId == previousId) ?? Groups[0];

                SelectedGroup = target;
            }
            finally
            {
                _suppressFilter = false;
            }
        }

        /// <summary>订阅全部节点（OpcUaService 内部按 NodeId 去重，重复调用安全）</summary>
        private async Task SubscribeNodesAsync()
        {
            if (_allNodes.Count == 0)
                return;

            try
            {
                await _opcUaService.SubscribeNodesAsync(_allNodes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "订阅 OPC 节点失败");
            }
        }

        // ══════════════════════════════════════════
        //  查询 / 分页
        // ══════════════════════════════════════════

        /// <summary>
        /// 按当前条件过滤节点并刷新表格。
        /// </summary>
        /// <param name="resetPage">条件变化时回到第 1 页</param>
        private void ApplyFilter(bool resetPage = true)
        {
            if (resetPage)
                PageIndex = 1;

            IEnumerable<OpcNodeConfig> query = _allNodes;

            if (SelectedGroup is { IsAll: false } group)
            {
                query = group.GroupId.HasValue
                    ? query.Where(n => n.GroupId == group.GroupId)
                    : query.Where(n => n.GroupId == null);
            }

            var keyword = Keyword?.Trim() ?? string.Empty;
            if (keyword.Length > 0)
            {
                query = query.Where(n =>
                    n.NodeId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (n.Description ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            query = WritableFilterIndex switch
            {
                1 => query.Where(n => n.IsWritable),
                2 => query.Where(n => !n.IsWritable),
                _ => query
            };

            query = RealtimeFilterIndex switch
            {
                1 => query.Where(n => n.IsShowInRealtime),
                2 => query.Where(n => !n.IsShowInRealtime),
                _ => query
            };

            _filteredNodes = query.ToList();
            TotalCount = _filteredNodes.Count;

            RefreshPage();
            UpdateFilterState(keyword);
        }

        /// <summary>按 PageIndex / PageSize 从过滤结果切片，重建当前页</summary>
        private void RefreshPage()
        {
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));

            if (PageIndex > TotalPages)
                PageIndex = TotalPages;
            if (PageIndex < 1)
                PageIndex = 1;

            var skip = (PageIndex - 1) * PageSize;

            PageNodes.Clear();
            foreach (var node in _filteredNodes.Skip(skip).Take(PageSize))
                PageNodes.Add(node);

            CanGoPrevious = PageIndex > 1;
            CanGoNext = PageIndex < TotalPages;
            HasNoResult = TotalCount == 0;

            PageInfoText = TotalCount == 0
                ? string.Format(LocalizationService.Instance["OpcNode_TotalFormat"], 0)
                : $"{string.Format(LocalizationService.Instance["OpcNode_TotalFormat"], TotalCount)} · " +
                  string.Format(LocalizationService.Instance["OpcNode_RangeFormat"], skip + 1, skip + PageNodes.Count);

            BuildPageNumbers();
        }

        /// <summary>生成页码按钮：首页、末页、当前页前后各 2 页，其余用省略号折叠</summary>
        private void BuildPageNumbers()
        {
            PageNumbers.Clear();

            if (TotalPages <= 1)
                return;

            var pages = new SortedSet<int> { 1, TotalPages };
            for (var page = PageIndex - 2; page <= PageIndex + 2; page++)
            {
                if (page >= 1 && page <= TotalPages)
                    pages.Add(page);
            }

            int? previous = null;
            foreach (var page in pages)
            {
                if (previous.HasValue && page - previous.Value > 1)
                {
                    PageNumbers.Add(new PageNumberItem { Text = "…", IsEllipsis = true });
                }

                PageNumbers.Add(new PageNumberItem
                {
                    Text = page.ToString(),
                    PageIndex = page,
                    IsCurrent = page == PageIndex
                });

                previous = page;
            }
        }

        /// <summary>更新“重置”按钮可用性与条件标签</summary>
        private void UpdateFilterState(string keyword)
        {
            var isGroupFiltered = SelectedGroup is { IsAll: false };
            var isWritableFiltered = WritableFilterIndex != 0;
            var isRealtimeFiltered = RealtimeFilterIndex != 0;

            HasActiveFilter = keyword.Length > 0 || isGroupFiltered || isWritableFiltered || isRealtimeFiltered;

            HasKeywordChip = keyword.Length > 0;
            KeywordChipText = string.Format(LocalizationService.Instance["OpcNode_FilterKeywordLabel"], keyword);

            HasGroupChip = isGroupFiltered;
            GroupChipText = string.Format(
                LocalizationService.Instance["OpcNode_FilterGroupLabel"], SelectedGroup?.Name ?? string.Empty);

            HasWritableChip = isWritableFiltered;
            WritableChipText = string.Format(
                LocalizationService.Instance["OpcNode_FilterWritableLabel"],
                WritableFilterOptions.ElementAtOrDefault(WritableFilterIndex) ?? string.Empty);

            HasRealtimeChip = isRealtimeFiltered;
            RealtimeChipText = string.Format(
                LocalizationService.Instance["OpcNode_FilterRealtimeLabel"],
                RealtimeFilterOptions.ElementAtOrDefault(RealtimeFilterIndex) ?? string.Empty);
        }

        partial void OnKeywordChanged(string value)
        {
            if (_suppressFilter)
                return;

            _keywordDebounceTimer.Stop();
            _keywordDebounceTimer.Start();
        }

        partial void OnWritableFilterIndexChanged(int value)
        {
            if (_suppressFilter)
                return;

            ApplyFilter();
        }

        partial void OnRealtimeFilterIndexChanged(int value)
        {
            if (_suppressFilter)
                return;

            ApplyFilter();
        }

        partial void OnSelectedGroupChanged(OpcNodeGroupItem? value)
        {
            if (_suppressFilter)
                return;

            ApplyFilter();
        }

        partial void OnPageSizeChanged(int value)
        {
            if (_suppressFilter)
                return;

            PageIndex = 1;
            ApplyFilter(resetPage: false);
        }

        private void OnKeywordDebounceTick(object? sender, EventArgs e)
        {
            _keywordDebounceTimer.Stop();
            ApplyFilter();
        }

        [RelayCommand]
        private void Search()
        {
            _keywordDebounceTimer.Stop();
            ApplyFilter();
        }

        [RelayCommand]
        private void ResetSearch()
        {
            _keywordDebounceTimer.Stop();

            _suppressFilter = true;
            try
            {
                Keyword = string.Empty;
                WritableFilterIndex = 0;
                RealtimeFilterIndex = 0;
                SelectedGroup = Groups.FirstOrDefault(g => g.IsAll) ?? Groups.FirstOrDefault();
            }
            finally
            {
                _suppressFilter = false;
            }

            ApplyFilter();
        }

        [RelayCommand]
        private void ClearKeywordChip()
        {
            _keywordDebounceTimer.Stop();
            Keyword = string.Empty;
            ApplyFilter();
        }

        [RelayCommand]
        private void ClearGroupChip()
        {
            _suppressFilter = true;
            try
            {
                SelectedGroup = Groups.FirstOrDefault(g => g.IsAll) ?? Groups.FirstOrDefault();
            }
            finally
            {
                _suppressFilter = false;
            }

            ApplyFilter();
        }

        [RelayCommand]
        private void ClearWritableChip() => WritableFilterIndex = 0;

        [RelayCommand]
        private void ClearRealtimeChip() => RealtimeFilterIndex = 0;

        [RelayCommand]
        private void GoToPage(int? pageIndex)
        {
            if (!pageIndex.HasValue || pageIndex.Value < 1 || pageIndex.Value > TotalPages)
                return;

            if (pageIndex.Value == PageIndex)
                return;

            PageIndex = pageIndex.Value;
            RefreshPage();
        }

        [RelayCommand]
        private void PreviousPage() => GoToPage(PageIndex - 1);

        [RelayCommand]
        private void NextPage() => GoToPage(PageIndex + 1);

        [RelayCommand]
        private async Task RefreshAsync() => await ReloadAsync();

        // ══════════════════════════════════════════
        //  节点增删改
        // ══════════════════════════════════════════

        [RelayCommand]
        private async Task CreateNodeAsync()
        {
            var groups = Groups.Where(g => g.IsManageable).ToList();
            var viewModel = new OpcNodeEditViewModel(
                groups,
                null,
                nodeId => _nodeAppService.NodeIdExistsAsync(nodeId));

            if (!ShowDialog<OpcNodeEditWindow, OpcNodeEditViewModel>(viewModel))
                return;

            try
            {
                var created = await _nodeAppService.CreateNodeAsync(new OpcNodeDto
                {
                    NodeId = viewModel.NodeId.Trim(),
                    Description = viewModel.Description,
                    GroupId = viewModel.SelectedGroupOption?.GroupId,
                    IsWritable = viewModel.IsWritable,
                    IsShowInRealtime = viewModel.IsShowInRealtime
                });

                await ReloadAsync();

                // 立即订阅新节点，无需等待下一次刷新
                if (_nodeInstanceMap.TryGetValue(created.NodeId, out var node))
                    await _opcUaService.SubscribeNodesAsync(new[] { node });

                _logger.LogInformation("已创建 OPC 节点: {NodeId}", created.NodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建 OPC 节点失败");
                ShowError(string.Format(LocalizationService.Instance["OpcNode_CreateNodeFailed"], ex.Message));
            }
        }

        [RelayCommand]
        private async Task EditNodeAsync(OpcNodeConfig? node)
        {
            if (node == null)
                return;

            var groups = Groups.Where(g => g.IsManageable).ToList();
            var viewModel = new OpcNodeEditViewModel(groups, node);

            if (!ShowDialog<OpcNodeEditWindow, OpcNodeEditViewModel>(viewModel))
                return;

            try
            {
                // 编辑时可能调整了分组，需要按当前数据校验分组是否仍存在
                var groupId = viewModel.SelectedGroupOption?.GroupId;
                if (groupId.HasValue && Groups.All(g => g.GroupId != groupId))
                    groupId = null;

                await _nodeAppService.UpdateNodeAsync(new OpcNodeDto
                {
                    Id = node.Id,
                    NodeId = node.NodeId,
                    Description = viewModel.Description,
                    GroupId = groupId,
                    IsWritable = viewModel.IsWritable,
                    IsShowInRealtime = viewModel.IsShowInRealtime
                });

                await ReloadAsync();
                _logger.LogInformation("已更新 OPC 节点: {NodeId}", node.NodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新 OPC 节点失败: {NodeId}", node.NodeId);
                ShowError(string.Format(LocalizationService.Instance["OpcNode_UpdateNodeFailed"], ex.Message));
            }
        }

        [RelayCommand]
        private async Task DeleteNodeAsync(OpcNodeConfig? node)
        {
            if (node == null)
                return;

            var confirmed = ConfirmDialogWindow.Show(
                LocalizationService.Instance["OpcNode_DeleteTitle"],
                string.Format(LocalizationService.Instance["OpcNode_DeleteNodeMessage"], node.NodeId),
                LocalizationService.Instance["OpcNode_DeleteConfirm"],
                LocalizationService.Instance["Common_Cancel"]);

            if (!confirmed)
                return;

            await DeleteNodesInternalAsync(new[] { node });
        }

        [RelayCommand]
        private async Task DeleteSelectedNodesAsync(IList? selectedNodes)
        {
            var nodes = selectedNodes?.OfType<OpcNodeConfig>().ToList() ?? new List<OpcNodeConfig>();

            if (nodes.Count == 0)
            {
                ShowWarning(LocalizationService.Instance["OpcNode_SelectNodeFirst"]);
                return;
            }

            var message = nodes.Count == 1
                ? string.Format(LocalizationService.Instance["OpcNode_DeleteNodeMessage"], nodes[0].NodeId)
                : string.Format(LocalizationService.Instance["OpcNode_DeleteNodeBatchMessage"], nodes.Count);

            var confirmed = ConfirmDialogWindow.Show(
                LocalizationService.Instance["OpcNode_DeleteTitle"],
                message,
                LocalizationService.Instance["OpcNode_DeleteConfirm"],
                LocalizationService.Instance["Common_Cancel"]);

            if (!confirmed)
                return;

            await DeleteNodesInternalAsync(nodes);
        }

        private async Task DeleteNodesInternalAsync(IReadOnlyList<OpcNodeConfig> nodes)
        {
            try
            {
                var deleted = await _nodeAppService.DeleteNodesAsync(nodes.Select(n => n.Id));

                // 不调用 UnsubscribeAll，避免打断控制页的实时订阅；
                // 实例缓存也保留，便于同一 NodeId 重新添加后继续接收实时值
                await ReloadAsync();

                _logger.LogInformation("已删除 {Count} 个 OPC 节点", deleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除 OPC 节点失败");
                ShowError(string.Format(LocalizationService.Instance["OpcNode_DeleteFailed"], ex.Message));
            }
        }

        // ══════════════════════════════════════════
        //  分组管理
        // ══════════════════════════════════════════

        [RelayCommand]
        private async Task CreateGroupAsync()
        {
            var viewModel = new OpcNodeGroupEditViewModel(
                null,
                name => GroupNameExistsAsync(name, 0));

            if (!ShowDialog<OpcNodeGroupEditWindow, OpcNodeGroupEditViewModel>(viewModel))
                return;

            try
            {
                var created = await _nodeAppService.CreateGroupAsync(viewModel.Name.Trim(), viewModel.Description);
                await ReloadAsync();

                // 新建后自动定位到该分组
                var target = Groups.FirstOrDefault(g => g.GroupId == created.Id);
                if (target != null)
                    SelectedGroup = target;

                _logger.LogInformation("已创建 OPC 节点分组: {Name}", created.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建 OPC 节点分组失败");
                ShowError(string.Format(LocalizationService.Instance["OpcNode_GroupSaveFailed"], ex.Message));
            }
        }

        [RelayCommand]
        private async Task RenameGroupAsync(OpcNodeGroupItem? group)
        {
            if (group is not { IsManageable: true })
                return;

            var viewModel = new OpcNodeGroupEditViewModel(
                group,
                name => GroupNameExistsAsync(name, group.GroupId ?? 0));

            if (!ShowDialog<OpcNodeGroupEditWindow, OpcNodeGroupEditViewModel>(viewModel))
                return;

            try
            {
                await _nodeAppService.RenameGroupAsync(
                    group.GroupId!.Value, viewModel.Name.Trim(), viewModel.Description);
                await ReloadAsync();

                _logger.LogInformation("已重命名 OPC 节点分组: {Name}", viewModel.Name.Trim());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重命名 OPC 节点分组失败");
                ShowError(string.Format(LocalizationService.Instance["OpcNode_GroupSaveFailed"], ex.Message));
            }
        }

        [RelayCommand]
        private async Task DeleteGroupAsync(OpcNodeGroupItem? group)
        {
            if (group is not { IsManageable: true })
                return;

            var confirmed = ConfirmDialogWindow.Show(
                LocalizationService.Instance["OpcNode_DeleteGroupTitle"],
                string.Format(
                    LocalizationService.Instance["OpcNode_DeleteGroupMessage"], group.Name, group.NodeCount),
                LocalizationService.Instance["OpcNode_DeleteConfirm"],
                LocalizationService.Instance["Common_Cancel"]);

            if (!confirmed)
                return;

            try
            {
                await _nodeAppService.DeleteGroupAsync(group.GroupId!.Value);

                // 若当前过滤的正是被删分组，重置为“全部节点”
                if (SelectedGroup?.GroupId == group.GroupId)
                {
                    _suppressFilter = true;
                    try
                    {
                        SelectedGroup = Groups.FirstOrDefault(g => g.IsAll);
                    }
                    finally
                    {
                        _suppressFilter = false;
                    }
                }

                await ReloadAsync();
                _logger.LogInformation("已删除 OPC 节点分组: {Name}", group.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除 OPC 节点分组失败: {Name}", group.Name);
                ShowError(string.Format(LocalizationService.Instance["OpcNode_GroupSaveFailed"], ex.Message));
            }
        }

        private async Task<bool> GroupNameExistsAsync(string name, int excludeGroupId)
        {
            var groups = await _nodeAppService.GetGroupsAsync();
            return groups.Any(g =>
                g.Id != excludeGroupId &&
                string.Equals(g.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // ══════════════════════════════════════════
        //  Excel 导入导出
        // ══════════════════════════════════════════

        [RelayCommand]
        private async Task ExportExcelAsync()
        {
            if (_filteredNodes.Count == 0)
            {
                ShowWarning(LocalizationService.Instance["OpcNode_ExportEmpty"]);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = LocalizationService.Instance["OpcNode_Export"],
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"OPC节点_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                IsBusy = true;

                var rows = _filteredNodes.Select(n => new OpcNodeDto
                {
                    Id = n.Id,
                    NodeId = n.NodeId,
                    Description = n.Description,
                    GroupId = n.GroupId,
                    GroupName = n.GroupName,
                    IsWritable = n.IsWritable,
                    IsShowInRealtime = n.IsShowInRealtime
                }).ToList();

                await _excelService.ExportAsync(
                    dialog.FileName, rows, LocalizationService.Instance["OpcNode_Ungrouped"]);

                ShowInfo(string.Format(
                    LocalizationService.Instance["OpcNode_ExportSuccess"], rows.Count, dialog.FileName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出 OPC 节点失败");
                ShowError(string.Format(LocalizationService.Instance["OpcNode_ExportFailed"], ex.Message));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ImportExcelAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = LocalizationService.Instance["OpcNode_Import"],
                Filter = "Excel 文件 (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                IsBusy = true;

                var parsed = await _excelService.ReadAsync(dialog.FileName);

                if (parsed.Rows.Count == 0 && parsed.Errors.Count == 0)
                {
                    ShowWarning(LocalizationService.Instance["OpcNode_ImportEmpty"]);
                    return;
                }

                // 预览统计：按 NodeId 是否已存在区分“新增/更新”
                var existingNodeIds = _allNodes
                    .Select(n => n.NodeId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var addedCount = parsed.Rows.Count(r => !existingNodeIds.Contains(r.NodeId));
                var updatedCount = parsed.Rows.Count - addedCount;

                var errors = parsed.Errors.Take(MaxDisplayedImportErrors).ToList();
                if (parsed.Errors.Count > MaxDisplayedImportErrors)
                {
                    errors.Add(string.Format(
                        LocalizationService.Instance["OpcNode_ImportErrorsMore"],
                        parsed.Errors.Count - MaxDisplayedImportErrors));
                }

                var previewViewModel = new OpcNodeImportPreviewViewModel(
                    dialog.FileName, addedCount, updatedCount, parsed.Errors.Count, errors);

                if (!ShowDialog<OpcNodeImportPreviewWindow, OpcNodeImportPreviewViewModel>(previewViewModel))
                    return;

                var result = await _nodeAppService.ImportAsync(parsed.Rows);
                await ReloadAsync();

                ShowInfo(string.Format(
                    LocalizationService.Instance["OpcNode_ImportResultFormat"],
                    result.AddedCount, result.UpdatedCount, result.SkippedCount));

                _logger.LogInformation(
                    "OPC 节点导入完成：新增 {Added}，更新 {Updated}，跳过 {Skipped}",
                    result.AddedCount, result.UpdatedCount, result.SkippedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入 OPC 节点失败");
                ShowError(string.Format(LocalizationService.Instance["OpcNode_ImportFailed"], ex.Message));
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ══════════════════════════════════════════
        //  弹窗辅助
        // ══════════════════════════════════════════

        /// <summary>通过 DI 创建弹窗并接入 ViewModel 的关闭请求，返回 true 表示用户确认</summary>
        private bool ShowDialog<TWindow, TViewModel>(TViewModel viewModel)
            where TWindow : Window
            where TViewModel : class
        {
            var window = ActivatorUtilities.CreateInstance<TWindow>(_serviceProvider, viewModel);
            window.Owner = Application.Current.MainWindow;

            var result = false;

            switch (viewModel)
            {
                case ConfirmDialogViewModel confirm:
                    confirm.CloseRequested += (_, ok) => { result = ok; window.Close(); };
                    break;
                case OpcNodeEditViewModel edit:
                    edit.CloseRequested += (_, ok) => { result = ok; window.Close(); };
                    break;
                case OpcNodeGroupEditViewModel group:
                    group.CloseRequested += (_, ok) => { result = ok; window.Close(); };
                    break;
                case OpcNodeImportPreviewViewModel preview:
                    preview.CloseRequested += (_, ok) => { result = ok; window.Close(); };
                    break;
            }

            window.ShowDialog();
            return result;
        }

        private static void ShowInfo(string message) => MessageBox.Show(
            message, LocalizationService.Instance["Msg_Title_Info"],
            MessageBoxButton.OK, MessageBoxImage.Information);

        private static void ShowWarning(string message) => MessageBox.Show(
            message, LocalizationService.Instance["Msg_Title_Info"],
            MessageBoxButton.OK, MessageBoxImage.Warning);

        private static void ShowError(string message) => MessageBox.Show(
            message, LocalizationService.Instance["Msg_Title_Error"],
            MessageBoxButton.OK, MessageBoxImage.Error);

        // ══════════════════════════════════════════
        //  释放
        // ══════════════════════════════════════════

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _keywordDebounceTimer.Stop();
            _keywordDebounceTimer.Tick -= OnKeywordDebounceTick;

            GC.SuppressFinalize(this);
        }
    }
}
