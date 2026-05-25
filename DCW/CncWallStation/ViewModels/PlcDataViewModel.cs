using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace CncWallStation.ViewModels
{
    public partial class PlcDataViewModel : ObservableObject
    {
        private readonly IPlcDataAppService _plcDataAppService;
        private readonly IWallAppService _wallAppService;
        private readonly ILogger<PlcDataViewModel> _logger;

        public PlcDataViewModel(
            IPlcDataAppService plcDataAppService,
            IWallAppService wallAppService,
            ILogger<PlcDataViewModel> logger)
        {
            _plcDataAppService = plcDataAppService;
            _wallAppService = wallAppService;
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

        // ==================== 特征分组 ====================

        /// <summary>所有特征分组</summary>
        public ObservableCollection<PlcFeatureGroupDto> FeatureGroups { get; } = new();

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
                        $"未找到墙体 \"{SearchWallId}\"，请检查墙体ID是否正确。",
                        "查询失败",
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
                        "数据不完整",
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
                    "错误",
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

        /// <summary>下发指令</summary>
        [RelayCommand]
        private async Task SendAsync()
        {
            _logger.LogInformation("下发PLC指令: WallId={WallId}", WallInfo?.WallId);
            // TODO: 对接生产设备下发接口
            await Task.CompletedTask;
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

            // 生成分组指令（写入 PlcInstructionEntity 表）
            var groups = await _plcDataAppService.GeneratePlcInstructionsGroupedAsync(WallInfo.Id);

            // 转换为 DTO
            LoadFeatureGroups(groups);

            // 统计
            RecalculateStatistics();

            // 渲染 3D
            await Render3DAsync();
        }

        /// <summary>保存草稿</summary>
        [RelayCommand]
        private async Task SaveDraftAsync()
        {
            if (WallInfo == null) return;

            try
            {
                var updatedBy = Environment.UserName;
                var entities = new List<PlcInstructionEntity>();

                int sortOrder = 0;
                foreach (var group in FeatureGroups)
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
            FeatureGroups.Clear();
            CurrentInstructions.Clear();
            Statistics = new PlcStatisticsDto();
            SelectedGroup = null;
            SelectedInstruction = null;

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

        private void LoadFeatureGroups(List<Plcs.PlcFeatureGroup> groups)
        {
            FeatureGroups.Clear();
            CurrentInstructions.Clear();

            foreach (var group in groups)
            {
                var dtoInstructions = new List<PlcInstructionDto>();
                int sortOrder = 0;
                foreach (var inst in group.Instructions)
                {
                    dtoInstructions.Add(PlcInstructionDto.FromPlcInstruction(inst, sortOrder++));
                }

                var dto = new PlcFeatureGroupDto
                {
                    HandlerName = group.HandlerName,
                    FeatureName = group.FeatureName,
                    InstructionCount = group.Instructions.Count,
                    Instructions = dtoInstructions
                };
                FeatureGroups.Add(dto);
            }
        }

        private void LoadInstructionsFromEntities(List<PlcInstructionEntity> entities)
        {
            FeatureGroups.Clear();
            CurrentInstructions.Clear();

            // 按 HandlerName 分组
            var grouped = entities
                .GroupBy(e => e.HandlerName)
                .ToList();

            foreach (var group in grouped)
            {
                var featureName = Plcs.PlcFeatureGroup.FeatureNameMap
                    .TryGetValue(group.Key, out var name) ? name : group.Key;

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

                var dto = new PlcFeatureGroupDto
                {
                    HandlerName = group.Key,
                    FeatureName = featureName,
                    InstructionCount = instructions.Count,
                    Instructions = instructions
                };
                FeatureGroups.Add(dto);
            }
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
