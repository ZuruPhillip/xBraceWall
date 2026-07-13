using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.EntityFrameworkCore;
using CncWallStation.Localization;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CncWallStation.Services.OpcUa;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace CncWallStation.ViewModels
{
    public partial class PlcDataViewModel : ObservableObject
    {
        private readonly IPlcDataAppService _plcDataAppService;
        private readonly IWallAppService _wallAppService;
        private readonly IOpcUaService _opcUaService;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly ILogger<PlcDataViewModel> _logger;

        private const string OPC_NODE_ID_PREFIX = "ns=2;s=unit/MCCUnit_35.InDATA_CNC_P.LineDef";

        /// <summary>指令行头字母：T, F, D, X0, Y0, Z0, X1, Y1, Z1</summary>
        private static readonly string[] LineHeaders = { "T", "F", "D", "X0", "Y0", "Z0", "X1", "Y1", "Z1" };

        public PlcDataViewModel(
            IPlcDataAppService plcDataAppService,
            IWallAppService wallAppService,
            IOpcUaService opcUaService,
            IDbContextFactory<AppDbContext> dbContextFactory,
            ILogger<PlcDataViewModel> logger)
        {
            _plcDataAppService = plcDataAppService;
            _wallAppService = wallAppService;
            _opcUaService = opcUaService;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        // ==================== 搜索 ====================

        [ObservableProperty]
        private string _searchWallId = string.Empty;

        /// <summary>是否已加载墙体</summary>
        [ObservableProperty]
        private bool _isWallLoaded;

        // ==================== 墙体信息 ====================

        [ObservableProperty]
        private WallInfoDto? _wallInfo;

        /// <summary>是否已审核（驱动 DataGrid 只读 + 按钮状态）</summary>
        [ObservableProperty]
        private bool _isAudited;

        // ==================== 墙体实际尺寸 ====================

        /// <summary>墙体实际长度（mm），同步写入 WallHandler 分组 X0</summary>
        [ObservableProperty]
        private float _wallActualLength;

        /// <summary>墙体实际宽度（mm），同步写入 WallHandler 分组 Y0</summary>
        [ObservableProperty]
        private float _wallActualWidth;

        /// <summary>墙体实际高度/厚度（mm），同步写入 WallHandler 分组 Z0</summary>
        [ObservableProperty]
        private float _wallActualHeight;

        /// <summary>防止同步时递归触发</summary>
        private bool _isSyncingDimensions;

        partial void OnWallActualLengthChanged(float value)
        {
            if (!_isSyncingDimensions) SyncWallDimensions();
        }

        partial void OnWallActualWidthChanged(float value)
        {
            if (!_isSyncingDimensions) SyncWallDimensions();
        }

        partial void OnWallActualHeightChanged(float value)
        {
            if (!_isSyncingDimensions) SyncWallDimensions();
        }

        // ==================== 特征分组 ====================

        /// <summary>正面特征分组 DTO 列表</summary>
        private List<PlcFeatureGroupDto> _frontGroupDtos = new();

        /// <summary>反面特征分组 DTO 列表</summary>
        private List<PlcFeatureGroupDto> _backGroupDtos = new();

        /// <summary>当前显示的特征分组（根据 SelectedSide 切换）</summary>
        public ObservableCollection<PlcFeatureGroupDto> FeatureGroups { get; } = new();

        /// <summary>当前选中正反面（0=正面, 1=反面）</summary>
        [ObservableProperty]
        private int _selectedSide;

        /// <summary>响应正反面切换，刷新 FeatureGroups</summary>
        partial void OnSelectedSideChanged(int value)
        {
            RefreshFeatureGroups();
            _ = Render3DAsync();
        }

        /// <summary>根据 SelectedSide 刷新 FeatureGroups 和 CurrentInstructions</summary>
        private void RefreshFeatureGroups()
        {
            var source = SelectedSide == 0 ? _frontGroupDtos : _backGroupDtos;

            FeatureGroups.Clear();
            CurrentInstructions.Clear();
            SelectedGroup = null;

            foreach (var dto in source)
            {
                FeatureGroups.Add(dto);
            }

            ReadWallDimensionsFromInstructions();
            RecalculateStatistics();
        }

        /// <summary>当前选中特征组</summary>
        [ObservableProperty]
        private PlcFeatureGroupDto? _selectedGroup;

        /// <summary>响应特征组切换，填充当前指令集</summary>
        partial void OnSelectedGroupChanged(PlcFeatureGroupDto? value)
        {
            CurrentInstructions.Clear();
            SelectedInstruction = null;

            if (value != null)
            {
                foreach (var inst in value.Instructions)
                {
                    CurrentInstructions.Add(inst);
                }
            }
        }

        /// <summary>当前特征的指令集</summary>
        public ObservableCollection<PlcInstructionDto> CurrentInstructions { get; } = new();

        /// <summary>当前标签页索引（0=指令表格，1=3D渲染）</summary>
        [ObservableProperty]
        private int _selectedTabIndex;

        // ==================== 选中指令（驱动 3D 高亮） ====================

        private int _selectedInstructionIndex = -1;

        /// <summary>选中的指令（DataGrid 选中项）</summary>
        [ObservableProperty]
        private PlcInstructionDto? _selectedInstruction;

        partial void OnSelectedInstructionChanged(PlcInstructionDto? value)
        {
            if (value != null)
            {
                int idx = CurrentInstructions.IndexOf(value);
                _selectedInstructionIndex = idx;
                _ = HighlightInstructionIn3D(idx);
            }
            else
            {
                _selectedInstructionIndex = -1;
                _ = ClearHighlightIn3D();
            }
        }

        // ==================== 统计 ====================

        [ObservableProperty]
        private PlcStatisticsDto _statistics = new();

        // ==================== WebView2 委托（由 Page 注入） ====================

        public Func<string, Task>? ExecuteScriptAsync { get; set; }
        public Func<string, Task>? NavigateToHtml { get; set; }

        // ==================== 命令 ====================

        /// <summary>搜索墙体</summary>
        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchWallId))
            {
                _logger.LogWarning("搜索墙体Id为空");
                return;
            }

            try
            {
                var wallInfo = await _plcDataAppService.GetWallInfoAsync(SearchWallId.Trim());
                if (wallInfo == null)
                {
                    System.Windows.MessageBox.Show(
                        string.Format(LocalizationService.Instance["Msg_WallNotFound"], SearchWallId),
                        LocalizationService.Instance["Msg_Title_Warning"],
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    IsWallLoaded = false;
                    return;
                }

                // MomJsonData 为空时无法生成 PLC 指令（需要先执行管线操作）
                if (string.IsNullOrWhiteSpace(wallInfo.MomJsonData))
                {
                    System.Windows.MessageBox.Show(
                        $"墙体 \"{SearchWallId}\" 的 MOM 数据尚未生成，无法计算 PLC 指令。\n\n" +
                        "请先在 WallListPage 中对该墙体执行「管线」操作，\n" +
                        "完成 BimJSON → MomJSON 转换后再试。",
                        LocalizationService.Instance["Msg_Title_DataIncomplete"],
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    IsWallLoaded = false;
                    return;
                }

                WallInfo = wallInfo;
                IsAudited = wallInfo.IsAudited;
                IsWallLoaded = true;

                // 检查是否有已保存的指令
                var existing = await _plcDataAppService.LoadInstructionsAsync(wallInfo.Id);
                if (existing.Count > 0)
                {
                    LoadInstructionsFromEntities(existing);
                }
                else
                {
                    // 生成新指令
                    await RegenerateInternalAsync();
                }

                _logger.LogInformation("加载墙体成功: {WallId}", SearchWallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索墙体失败: {WallId}", SearchWallId);
                System.Windows.MessageBox.Show(
                    $"搜索墙体时发生异常：\n\n{ex.Message}",
                    LocalizationService.Instance["Msg_Title_Error"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                IsWallLoaded = false;
            }
        }

        /// <summary>审核</summary>
        [RelayCommand]
        private async Task AuditAsync()
        {
            if (WallInfo == null) return;

            try
            {
                var updatedBy = Environment.UserName;
                await _wallAppService.SetAuditStatusAsync(WallInfo.Id, (int)AuditStatus.已审核, updatedBy);

                WallInfo.AuditStatus = (int)AuditStatus.已审核;
                IsAudited = true;
                OnPropertyChanged(nameof(WallInfo));

                _logger.LogInformation("审核通过: WallId={WallId}", WallInfo.WallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "审核失败: WallId={WallId}", WallInfo?.WallId);
            }
        }

        /// <summary>反审核</summary>
        [RelayCommand]
        private async Task UnauditAsync()
        {
            if (WallInfo == null) return;

            try
            {
                var updatedBy = Environment.UserName;
                await _wallAppService.SetAuditStatusAsync(WallInfo.Id, (int)AuditStatus.未审核, updatedBy);

                WallInfo.AuditStatus = (int)AuditStatus.未审核;
                IsAudited = false;
                OnPropertyChanged(nameof(WallInfo));

                _logger.LogInformation("反审核: WallId={WallId}", WallInfo.WallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "反审核失败: WallId={WallId}", WallInfo?.WallId);
            }
        }

        /// <summary>下发指令：将所有指令按 OPC UA NodeId 格式批量写入 PLC</summary>
        [RelayCommand]
        private async Task SendAsync()
        {
            _logger.LogInformation("下发PLC指令: WallId={WallId}", WallInfo?.WallId);

            if (!IsWallLoaded || WallInfo == null)
            {
                _logger.LogWarning("下发指令失败：墙体未加载");
                System.Windows.MessageBox.Show(
                    "请先搜索并加载墙体数据。",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!_opcUaService.IsConnected)
            {
                _logger.LogWarning("下发指令失败：OPC UA 未连接");
                var result = System.Windows.MessageBox.Show(
                    "OPC UA 尚未连接到 PLC 设备，是否继续尝试发送？\n（发送将在连接恢复后可能不会自动重试）",
                    "OPC 未连接",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (result != System.Windows.MessageBoxResult.Yes)
                    return;
            }

            try
            {
                // 1. 扁平化所有分组的指令（正面在前、反面在后），从 0 开始编号索引 i
                var allInstructions = new List<PlcInstructionDto>();
                foreach (var group in _frontGroupDtos)
                {
                    allInstructions.AddRange(group.Instructions);
                }
                foreach (var group in _backGroupDtos)
                {
                    allInstructions.AddRange(group.Instructions);
                }

                if (allInstructions.Count == 0)
                {
                    _logger.LogWarning("下发指令失败：没有可发送的指令数据");
                    System.Windows.MessageBox.Show(
                        "没有可发送的指令数据。",
                        "提示",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 2. 为每条指令生成 9 个 NodeId -> value 键值对
                var nodeValues = new Dictionary<string, object>();

                for (int i = 0; i < allInstructions.Count; i++)
                {
                    var inst = allInstructions[i];
                    var values = new Dictionary<string, object>
                    {
                        ["T"] = inst.T,
                        ["F"] = inst.F,
                        ["D"] = inst.D,
                        ["X0"] = inst.X0,
                        ["Y0"] = inst.Y0,
                        ["Z0"] = inst.Z0,
                        ["X1"] = inst.X1,
                        ["Y1"] = inst.Y1,
                        ["Z1"] = inst.Z1
                    };

                    foreach (var kv in values)
                    {
                        var nodeId = $"{OPC_NODE_ID_PREFIX}[{i}].{kv.Key}";
                        nodeValues[nodeId] = kv.Value;
                    }
                }

                _logger.LogInformation(
                    "准备批量写入 OPC 节点: 指令数={InstructionCount}, 总节点数={NodeCount}",
                    allInstructions.Count, nodeValues.Count);

                // 3. 一次性批量写入到 PLC
                await _opcUaService.WriteNodesAsync(nodeValues);

                // 4. 持久化写入记录到 Opc 表（同一批次共享 GroupId）
                var groupId = Guid.NewGuid().ToString();
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                foreach (var kv in nodeValues)
                {
                    db.OpcWriteRecords.Add(new OpcWriteRecordEntity(
                        WallInfo.Id,
                        groupId,
                        kv.Key,
                        kv.Value?.ToString() ?? string.Empty));
                }
                await db.SaveChangesAsync();

                _logger.LogInformation(
                    "PLC 指令下发成功: WallId={WallId}, 总节点数={NodeCount}, GroupId={GroupId}",
                    WallInfo.WallId, nodeValues.Count, groupId);

                System.Windows.MessageBox.Show(
                    $"指令下发成功！\n\n共发送 {allInstructions.Count} 条指令，{nodeValues.Count} 个节点。",
                    "下发成功",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下发PLC指令失败: WallId={WallId}", WallInfo?.WallId);
                System.Windows.MessageBox.Show(
                    $"下发指令时发生异常：\n\n{ex.Message}",
                    "下发失败",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>重新生成 PLC 数据</summary>
        [RelayCommand]
        private async Task RegenerateAsync()
        {
            if (WallInfo == null) return;

            try
            {
                await RegenerateInternalAsync();
                _logger.LogInformation("重新生成PLC指令: WallId={WallId}", WallInfo.WallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新生成PLC指令失败: WallId={WallId}", WallInfo?.WallId);
                System.Windows.MessageBox.Show(
                    $"重新生成失败：\n\n{ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task RegenerateInternalAsync()
        {
            if (WallInfo == null) return;

            // 生成分组指令（含原点变换、正反面分类）
            var result = await _plcDataAppService.GeneratePlcInstructionsGroupedAsync(WallInfo.Id);

            // 转换为 DTO（正反面分别填充）
            LoadFeatureGroups(result);

            // 渲染 3D
            await Render3DAsync();
        }

        /// <summary>保存草稿（正反两面一起保存）</summary>
        [RelayCommand]
        private async Task SaveDraftAsync()
        {
            if (WallInfo == null) return;

            try
            {
                var updatedBy = Environment.UserName;
                var entities = new List<PlcInstructionEntity>();

                // 正面指令（Side=0）在前
                int sortOrder = 0;
                foreach (var group in _frontGroupDtos)
                {
                    foreach (var inst in group.Instructions)
                    {
                        entities.Add(new PlcInstructionEntity
                        {
                            WallId = WallInfo.Id,
                            T = inst.T,
                            F = inst.F,
                            D = inst.D,
                            X0 = inst.X0,
                            Y0 = inst.Y0,
                            Z0 = inst.Z0,
                            X1 = inst.X1,
                            Y1 = inst.Y1,
                            Z1 = inst.Z1,
                            SortOrder = sortOrder++,
                            Side = 0,
                            HandlerName = group.HandlerName,
                            FeatureName = group.FeatureName
                        });
                    }
                }

                // 反面指令（Side=1）在后
                foreach (var group in _backGroupDtos)
                {
                    foreach (var inst in group.Instructions)
                    {
                        entities.Add(new PlcInstructionEntity
                        {
                            WallId = WallInfo.Id,
                            T = inst.T,
                            F = inst.F,
                            D = inst.D,
                            X0 = inst.X0,
                            Y0 = inst.Y0,
                            Z0 = inst.Z0,
                            X1 = inst.X1,
                            Y1 = inst.Y1,
                            Z1 = inst.Z1,
                            SortOrder = sortOrder++,
                            Side = 1,
                            HandlerName = group.HandlerName,
                            FeatureName = group.FeatureName
                        });
                    }
                }

                await _plcDataAppService.SaveDraftAsync(WallInfo.Id, entities, updatedBy);

                // 重新加载以获取数据库生成的 Id
                var savedEntities = await _plcDataAppService.LoadInstructionsAsync(WallInfo.Id);
                LoadInstructionsFromEntities(savedEntities);

                _logger.LogInformation("保存PLC指令草稿成功: WallId={WallId}", WallInfo.WallId);
                System.Windows.MessageBox.Show("草稿保存成功。", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存PLC指令草稿失败: WallId={WallId}", WallInfo?.WallId);
                System.Windows.MessageBox.Show(
                    $"保存失败：\n\n{ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>清空面板</summary>
        [RelayCommand]
        private void ClearPanel()
        {
            WallInfo = null;
            IsWallLoaded = false;
            IsAudited = false;
            SearchWallId = string.Empty;
            _frontGroupDtos.Clear();
            _backGroupDtos.Clear();
            FeatureGroups.Clear();
            CurrentInstructions.Clear();
            Statistics = new PlcStatisticsDto();
            SelectedGroup = null;
            SelectedInstruction = null;
            SelectedSide = 0;

            _isSyncingDimensions = true;
            WallActualLength = 0;
            WallActualWidth = 0;
            WallActualHeight = 0;
            _isSyncingDimensions = false;

            _ = Clear3DAsync();

            _logger.LogInformation("清空PLC面板");
        }

        /// <summary>切换选中特征组</summary>
        [RelayCommand]
        private void SelectGroup(PlcFeatureGroupDto? group)
        {
            if (group == null) return;

            SelectedGroup = group;
            CurrentInstructions.Clear();
            SelectedInstruction = null;

            foreach (var inst in group.Instructions)
            {
                CurrentInstructions.Add(inst);
            }
        }

        // ==================== 3D 渲染帮助方法 ====================

        public async Task RenderInitialAsync()
        {
            if (NavigateToHtml == null) return;

            // 获取 HTML 路径（嵌入资源或文件系统）
            var htmlPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Resources", "PlcRender.html");

            if (System.IO.File.Exists(htmlPath))
            {
                var url = $"file:///{htmlPath.Replace("\\", "/")}";
                await NavigateToHtml(url);
            }
            else
            {
                _logger.LogWarning("PlcRender.html 未找到: {Path}", htmlPath);
            }
        }

        private async Task Render3DAsync()
        {
            if (ExecuteScriptAsync == null) return;

            // 收集所有指令的 JSON
            var allInstructions = new List<object>();

            foreach (var group in FeatureGroups)
            {
                foreach (var inst in group.Instructions)
                {
                    allInstructions.Add(new
                    {
                        T = inst.T,
                        F = inst.F,
                        X0 = inst.X0, Y0 = inst.Y0, Z0 = inst.Z0,
                        X1 = inst.X1, Y1 = inst.Y1, Z1 = inst.Z1,
                        group = group.FeatureName
                    });
                }
            }

            var json = JsonSerializer.Serialize(allInstructions);
            var script = $"renderInstructions({json});";

            // 先清空再渲染
            await ExecuteScriptAsync("clearInstructions();");
            await Task.Delay(200);
            await ExecuteScriptAsync(script);
        }

        private async Task HighlightInstructionIn3D(int index)
        {
            if (ExecuteScriptAsync == null || index < 0) return;
            await ExecuteScriptAsync($"highlightInstruction({index});");
        }

        private async Task ClearHighlightIn3D()
        {
            if (ExecuteScriptAsync == null) return;
            await ExecuteScriptAsync("clearHighlight();");
        }

        private async Task Clear3DAsync()
        {
            if (ExecuteScriptAsync == null) return;
            await ExecuteScriptAsync("clearInstructions();");
        }

        // ==================== 内部方法 ====================

        /// <summary>
        /// 将实际尺寸同步到正反两组 WallHandler 分组中所有指令的 X0/Y0/Z0
        /// </summary>
        private void SyncWallDimensions()
        {
            if (!IsWallLoaded) return;

            // 更新正面和反面两组的 WallHandler 指令
            UpdateWallHandlerDimensions(_frontGroupDtos);
            UpdateWallHandlerDimensions(_backGroupDtos);

            RecalculateStatistics();
        }

        private void UpdateWallHandlerDimensions(List<PlcFeatureGroupDto> groups)
        {
            var wallGroup = groups.FirstOrDefault(g => g.HandlerName == "WallHandler");
            if (wallGroup == null) return;

            foreach (var inst in wallGroup.Instructions)
            {
                inst.X0 = WallActualLength;
                inst.Y0 = WallActualWidth;
                inst.Z0 = WallActualHeight;
            }
        }

        /// <summary>
        /// 从 WallHandler 分组第一条指令中读取实际尺寸初始值
        /// </summary>
        private void ReadWallDimensionsFromInstructions()
        {
            var wallGroup = FeatureGroups
                .FirstOrDefault(g => g.HandlerName == "WallHandler");
            if (wallGroup == null || wallGroup.Instructions.Count == 0) return;

            var first = wallGroup.Instructions[0];
            _isSyncingDimensions = true;
            try
            {
                WallActualLength = first.X0;
                WallActualWidth = first.Y0;
                WallActualHeight = first.Z0;
            }
            finally
            {
                _isSyncingDimensions = false;
            }
        }

        private void LoadFeatureGroups(Plcs.PlcGenerationResult result)
        {
            _frontGroupDtos = ConvertToDtos(result.FrontGroups);
            _backGroupDtos = ConvertToDtos(result.BackGroups);

            // 默认显示正面
            _isSyncingDimensions = true;
            SelectedSide = 0;
            _isSyncingDimensions = false;

            RefreshFeatureGroups();
        }

        /// <summary>将 PlcFeatureGroup 列表转换为 DTO 列表</summary>
        private List<PlcFeatureGroupDto> ConvertToDtos(List<Plcs.PlcFeatureGroup> groups)
        {
            var dtos = new List<PlcFeatureGroupDto>();
            var isEn = Localization.LocalizationService.Instance.CurrentLanguage.StartsWith("en");

            foreach (var group in groups)
            {
                var dtoInstructions = new List<PlcInstructionDto>();
                int sortOrder = 0;
                foreach (var inst in group.Instructions)
                {
                    dtoInstructions.Add(PlcInstructionDto.FromPlcInstruction(inst, sortOrder++));
                }

                string? nameEn = null;
                string? name = null;
                bool found = isEn
                    ? Plcs.PlcFeatureGroup.FeatureNameMapEn.TryGetValue(group.HandlerName, out nameEn)
                    : Plcs.PlcFeatureGroup.FeatureNameMap.TryGetValue(group.HandlerName, out name);
                var featureName = found
                    ? (isEn ? nameEn! : name!)
                    : group.FeatureName;

                dtos.Add(new PlcFeatureGroupDto
                {
                    HandlerName = group.HandlerName,
                    FeatureName = featureName,
                    InstructionCount = group.Instructions.Count,
                    Instructions = dtoInstructions
                });
            }

            return dtos;
        }

        private void LoadInstructionsFromEntities(List<PlcInstructionEntity> entities)
        {
            // 按 Side 分组（0=正面, 1=反面），旧数据无 Side 默认为 0
            var frontEntities = entities.Where(e => e.Side == 0).ToList();
            var backEntities = entities.Where(e => e.Side == 1).ToList();

            _frontGroupDtos = ConvertEntitiesToDtos(frontEntities);
            _backGroupDtos = ConvertEntitiesToDtos(backEntities);

            // 默认显示正面
            _isSyncingDimensions = true;
            SelectedSide = 0;
            _isSyncingDimensions = false;

            RefreshFeatureGroups();
        }

        /// <summary>将实体列表按 HandlerName 分组并转换为 DTO 列表</summary>
        private List<PlcFeatureGroupDto> ConvertEntitiesToDtos(List<PlcInstructionEntity> entities)
        {
            var dtos = new List<PlcFeatureGroupDto>();
            var isEn = Localization.LocalizationService.Instance.CurrentLanguage.StartsWith("en");

            var grouped = entities.GroupBy(e => e.HandlerName).ToList();

            foreach (var group in grouped)
            {
                string? nameEn = null;
                string? name = null;
                bool found = isEn
                    ? Plcs.PlcFeatureGroup.FeatureNameMapEn.TryGetValue(group.Key, out nameEn)
                    : Plcs.PlcFeatureGroup.FeatureNameMap.TryGetValue(group.Key, out name);
                var featureName = found
                    ? (isEn ? nameEn! : name!)
                    : group.Key;

                var instructions = group
                    .OrderBy(e => e.SortOrder)
                    .Select(e =>
                    {
                        var dto = new PlcInstructionDto
                        {
                            Id = e.Id,
                            T = e.T,
                            F = e.F,
                            D = e.D,
                            X0 = e.X0,
                            Y0 = e.Y0,
                            Z0 = e.Z0,
                            X1 = e.X1,
                            Y1 = e.Y1,
                            Z1 = e.Z1,
                            SortOrder = e.SortOrder
                        };
                        return dto;
                    })
                    .ToList();

                dtos.Add(new PlcFeatureGroupDto
                {
                    HandlerName = group.Key,
                    FeatureName = featureName,
                    InstructionCount = instructions.Count,
                    Instructions = instructions
                });
            }

            return dtos;
        }

        private void RecalculateStatistics()
        {
            var stats = new PlcStatisticsDto();
            var allInstructions = new List<PlcInstructionDto>();

            foreach (var group in FeatureGroups)
            {
                allInstructions.AddRange(group.Instructions);
            }

            // 按 SortOrder 排序
            allInstructions = allInstructions.OrderBy(i => i.SortOrder).ToList();

            if (allInstructions.Count == 0)
            {
                Statistics = stats;
                return;
            }

            // 0. 总指令条数
            stats.TotalInstructionCount = allInstructions.Count;

            // 1. 换刀次数：统计 T 值变化的次数
            int toolChangeCount = 0;
            int lastT = allInstructions[0].T;
            for (int i = 1; i < allInstructions.Count; i++)
            {
                if (allInstructions[i].T != lastT)
                {
                    toolChangeCount++;
                    lastT = allInstructions[i].T;
                }
            }
            stats.ToolChangeCount = toolChangeCount;

            // 2. 总切削面积
            double totalArea = 0;
            foreach (var inst in allInstructions)
            {
                // 面积 = |(X1 - X0)| * |(Z1 - Z0)|（矩形面积）
                double area = Math.Abs(inst.X1 - inst.X0) * Math.Abs(inst.Z1 - inst.Z0);
                if (area > 0)
                    totalArea += area * inst.D;  // D 为重复次数
            }
            stats.TotalCuttingArea = totalArea;

            // 3. 预估工时
            stats.EstimatedHours = totalArea / PlcStatisticsDto.StandardCuttingRate;

            Statistics = stats;
        }
    }
}
