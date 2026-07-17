using CncWallStation.Localization;
using CncWallStation.Models;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.Services;
using CncWallStation.Services.Application;
using CncWallStation.VersionMappers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CncWallStation.ViewModels
{
    public partial class WallListPageViewModel : ObservableObject
    {
        private readonly ILogger<WallListPageViewModel> _logger;
        private readonly IWallAppService _wallAppService;
        private readonly IProjectAppService _projectAppService;
        private readonly IPipelineService _pipelineService;

        // ==================== 全量数据缓存（当前筛选结果） ====================
        private List<WallListItem> _filteredItems = new();

        // ==================== 分页属性 ====================
        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageSize = 20;

        [ObservableProperty]
        private int _totalItems;

        [ObservableProperty]
        private int _totalPages;

        [ObservableProperty]
        private ObservableCollection<WallListItem> _displayItems = new();

        // ==================== 选中项 ====================
        [ObservableProperty]
        private ObservableCollection<WallListItem> _selectedItems = new();

        [ObservableProperty]
        private bool? _isAllSelected = false;

        // ==================== 分页按钮状态 ====================
        [ObservableProperty]
        private bool _hasPreviousPage;

        [ObservableProperty]
        private bool _hasNextPage;

        // ==================== 筛选属性 ====================
        [ObservableProperty]
        private string _searchProjectName = string.Empty;

        [ObservableProperty]
        private string _searchWallId = string.Empty;

        [ObservableProperty]
        private string _searchWallName = string.Empty;

        [ObservableProperty]
        private int? _filterFloor;

        [ObservableProperty]
        private ObservableCollection<int> _availableFloors = new();

        [ObservableProperty]
        private ProcessStatus? _filterStatus;

        [ObservableProperty]
        private ProcessPriority? _filterPriority;

        [ObservableProperty]
        private PipelineStage? _filterPipelineStage;

		[ObservableProperty]
		private AuditStatus? _filterAuditStatus;

		[ObservableProperty]
		private DateTime? _filterDateFrom;

		[ObservableProperty]
		private DateTime? _filterDateTo;

        // ==================== 状态属性 ====================
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isEmpty;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string? _importProgressMessage;

        [ObservableProperty]
        private int _importProgressValue;

        [ObservableProperty]
        private int _importProgressMax = 1;

        // ==================== 排序 ====================
		[ObservableProperty]
		private string _sortField = nameof(WallListItem.EndProductionTime);

		[ObservableProperty]
		private bool _sortAscending;

        // ==================== 可用选项列表（用于下拉筛选） ====================
		public List<ProcessStatus> AllStatuses { get; } = new()
		{ ProcessStatus.待校验, ProcessStatus.待加工, ProcessStatus.加工中, ProcessStatus.已完成, ProcessStatus.异常, ProcessStatus.已质检 };

        public List<ProcessPriority> AllPriorities { get; } = new()
        { ProcessPriority.高, ProcessPriority.中, ProcessPriority.低 };

        public List<PipelineStage> AllPipelineStages { get; } = new()
        {
            PipelineStage.Imported, PipelineStage.BimInvalid,
            PipelineStage.ConversionFailed, PipelineStage.MomInvalid,
            PipelineStage.Ready
        };

        public List<AuditStatus> AllAuditStatuses { get; } = new()
        { AuditStatus.未审核, AuditStatus.已审核 };

        public List<int> PageSizeOptions { get; } = new() { 10, 20, 50, 100 };

        // ==================== 构造函数 ====================
        public WallListPageViewModel(
            ILogger<WallListPageViewModel> logger,
            IWallAppService wallAppService,
            IProjectAppService projectAppService,
            IPipelineService pipelineService,
            MainPageViewModel mainPageViewModel)
        {
            _logger = logger;
            _wallAppService = wallAppService;
            _projectAppService = projectAppService;
            _pipelineService = pipelineService;
            _mainPageViewModel = mainPageViewModel;

            Localization.LocalizationService.Instance.CultureChanged += OnCultureChanged;

            _ = LoadDataFromDbAsync();
        }

        private void OnCultureChanged(object? sender, string e)
        {
            // 强制刷新显示以触发所有 Status/AuditStatus 转换器重新评估
            if (_filteredItems.Count > 0)
                RefreshDisplay();
        }

        private readonly MainPageViewModel _mainPageViewModel;

        // ==================== 从数据库加载数据 ====================
        private async Task LoadDataFromDbAsync()
        {
            try
            {
                IsLoading = true;
                await ApplyFiltersAsync();
                await UpdateAvailableFloorsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库加载数据失败");
                HasError = true;
                ErrorMessage = $"数据库加载失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ==================== 命令：批量导入 ====================
        [RelayCommand]
        private async Task ImportAsync()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择房屋根文件夹（包含数字楼层子文件夹）",
                Multiselect = false
            };

            var result = dialog.ShowDialog();
            if (result != true || string.IsNullOrWhiteSpace(dialog.FolderName))
                return;

            IsLoading = true;
            ImportProgressValue = 0;
            ImportProgressMax = 0;
            ImportProgressMessage = "正在扫描文件夹结构...";

            try
            {
                var rootFolder = dialog.FolderName;
                var projectName = Path.GetFileName(rootFolder);

                // 扫描数字命名的楼层子文件夹
                var floorFolders = new List<(int FloorIndex, string FolderPath)>();
                foreach (var subDir in Directory.GetDirectories(rootFolder))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (int.TryParse(dirName, out var floorIndex))
                        floorFolders.Add((floorIndex, subDir));
                }

                if (floorFolders.Count == 0)
                {
                    ImportProgressMessage = "未找到有效的楼层文件夹";
                    MessageBox.Show("根目录下未找到任何数字命名的楼层子文件夹（如 0、1、2）。\n请确认所选文件夹结构是否正确。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await Task.Delay(1500);
                    return;
                }

                // 收集所有待处理的 .mjson 文件信息
                var fileEntries = new List<(string FilePath, int FloorIndex, string WallId)>();
                var skipReasons = new List<string>();

                foreach (var (floorIndex, floorPath) in floorFolders.OrderBy(f => f.FloorIndex))
                {
                    var wallsPath = Path.Combine(floorPath, "Walls");
                    if (!Directory.Exists(wallsPath))
                    {
                        skipReasons.Add($"楼层文件夹 \"{floorIndex}\" 下缺少 \"Walls\" 子文件夹，已跳过");
                        _logger.LogWarning("楼层 {FloorIndex} 缺少 Walls 子文件夹: {Path}", floorIndex, floorPath);
                        continue;
                    }

                    var mjsonFiles = Directory.GetFiles(wallsPath, "*.mjson");
                    foreach (var file in mjsonFiles)
                    {
                        var wallId = Path.GetFileNameWithoutExtension(file);
                        fileEntries.Add((file, floorIndex, wallId));
                    }
                }

                if (fileEntries.Count == 0)
                {
                    ImportProgressMessage = "未找到任何 .mjson 文件";
                    if (skipReasons.Count > 0)
                    {
                        var skipMsg = string.Join("\n", skipReasons);
                        _logger.LogWarning("导入跳过原因:\n{Reasons}", skipMsg);
                    }
                    MessageBox.Show("在楼层文件夹的 Walls 子目录中未找到任何 .mjson 文件。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    await Task.Delay(1500);
                    return;
                }

                ImportProgressMax = fileEntries.Count;
                int newCount = 0;
                int syncCount = 0;
                int skipAuditedCount = 0;
                int failCount = 0;
                int skipLgsWallCount = 0;
                var hostName = Environment.MachineName;
                var importedBy = Environment.UserName;

                // 预先批量查询已审核和已存在的 WallId
                var allWallIds = fileEntries.Select(f => f.WallId).ToList();
                var auditedWallIds = await _wallAppService.GetAuditedWallIdsAsync(allWallIds);
                var existingWallIds = await _wallAppService.GetExistingWallIdsAsync(allWallIds);

				// 创建项目批次
				var projectId = await _projectAppService.CreateProjectAsync(
                    projectName, rootFolder, hostName, importedBy, fileEntries.Count);

                var newWalls = new List<WallEntity>();

                for (int i = 0; i < fileEntries.Count; i++)
                {
                    var (filePath, floorIndex, wallId) = fileEntries[i];
                    var fileName = Path.GetFileName(filePath);
                    var floor = floorIndex + 1;

                    ImportProgressValue = i + 1;
                    ImportProgressMessage = $"正在处理: {fileName} ({i + 1}/{fileEntries.Count})";

                    try
                    {
                        var jsonContent = await File.ReadAllTextAsync(filePath);
                        var schemaVer = BimDataVersionResolver.ResolveVersion(jsonContent);

                        // 提取墙体名称
                        var wallName = ExtractWallName(jsonContent);

                        // 判断是否为 LGS Wall（含有 tracks 或 nogs 字段）
                        if (IsLgsWall(jsonContent))
                        {
                            skipLgsWallCount++;
                            _logger.LogInformation("LGS Wall 已过滤: WallId={WallId}, File={FileName}", wallId, fileName);
                            continue;
                        }

                        if (auditedWallIds.Contains(wallId))
                        {
                            // 已审核 → 跳过
                            skipAuditedCount++;
                            _logger.LogWarning("墙体已审核，跳过导入: WallId={WallId}", wallId);
                        }
                        else if (existingWallIds.Contains(wallId))
                        {
                            // 已存在且未审核 → 同步更新（替换BimJson+清空MomData+重置管线）
                            // 需要先找到已存在实体的 Id
                            var existing = await _wallAppService.GetDetailByWallIdAsync(wallId);
                            if (existing != null)
                            {
                                await _wallAppService.SyncBimDataAsync(existing.Id, jsonContent, schemaVer, wallName, importedBy);
                                syncCount++;
                                _logger.LogInformation("同步更新 BimData: WallId={WallId}, SchemaVer={SchemaVer}", wallId, schemaVer);
                            }
                        }
                        else
                        {
                            // 不存在 → 新建
                            var wall = new WallEntity(
                                projectId,
                                wallId,
                                projectName,
                                floor,
                                jsonContent,
                                wallName,
                                schemaVer);
                            newWalls.Add(wall);
                            newCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, "读取文件失败: {FilePath}", filePath);
                    }

                    if (i % 10 == 0)
                        await Task.Delay(1);
                }

                // 批量插入新墙体
                if (newWalls.Count > 0)
                {
                    await _wallAppService.InsertManyAsync(newWalls);
                }

                var statusMsg = $"导入完成：新增 {newCount}，同步更新 {syncCount}";
                if (skipLgsWallCount > 0)
                    statusMsg += $"，过滤LGS墙体 {skipLgsWallCount}";
                if (skipAuditedCount > 0)
                    statusMsg += $"，跳过已审核 {skipAuditedCount}";
                if (failCount > 0)
                    statusMsg += $"，失败 {failCount}";
                ImportProgressMessage = statusMsg;

                if (skipAuditedCount > 0 || skipLgsWallCount > 0)
                {
                    var detailMsg = "";
                    if (skipLgsWallCount > 0)
                        detailMsg += $"已过滤 {skipLgsWallCount} 面 LGS Wall 墙体（含 tracks/nogs 字段的轻钢龙骨墙体不导入）。\n";
                    if (skipAuditedCount > 0)
                        detailMsg += "已审核的墙体已被保护，未覆盖其数据。";
                    MessageBox.Show(
                        statusMsg + "\n\n" + detailMsg,
                        "导入结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await Task.Delay(2000);
                await ApplyFiltersAsync();
                await UpdateAvailableFloorsAsync();

                _logger.LogInformation("批量导入完成: 项目={ProjectName}, 新增{New}, 同步{Sync}, 过滤LGS{Lgs}, 跳过已审核{Skip}, 失败{Fail}",
                    projectName, newCount, syncCount, skipLgsWallCount, skipAuditedCount, failCount);
            }
            catch (Exception ex)
            {
                ImportProgressMessage = $"导入异常: {ex.Message}";
                _logger.LogError(ex, "批量导入异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ==================== 命令：单墙导入 ====================
        [RelayCommand]
        private async Task ImportSingleWallAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择单面墙的 .mjson 文件",
                Filter = "MJSON 文件|*.mjson|JSON 文件|*.json|所有文件|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
                return;

            IsLoading = true;

            try
            {
                var filePath = dialog.FileName;
                var fileName = Path.GetFileName(filePath);
                var wallId = Path.GetFileNameWithoutExtension(filePath);

                // 读取文件
                var jsonContent = await File.ReadAllTextAsync(filePath);
                var schemaVer = BimDataVersionResolver.ResolveVersion(jsonContent);
                var wallName = ExtractWallName(jsonContent);
                var importedBy = Environment.UserName;
                var hostName = Environment.MachineName;

                // 判断是否为 LGS Wall（含有 tracks 或 nogs 字段）
                if (IsLgsWall(jsonContent))
                {
                    MessageBox.Show($"墙体 \"{wallId}\" 为 LGS Wall，不允许导入。\n\n请使用专门的 LGS 墙体导入流程。",
                        "LGS Wall 已过滤", MessageBoxButton.OK, MessageBoxImage.Information);
                    _logger.LogInformation("LGS Wall 已过滤（单墙导入）: WallId={WallId}", wallId);
                    return;
                }

                // 检查是否已审核
                var auditedIds = await _wallAppService.GetAuditedWallIdsAsync(new[] { wallId });
                if (auditedIds.Contains(wallId))
                {
                    MessageBox.Show($"墙体 \"{wallId}\" 已审核，不允许覆盖导入。\n请先执行反审核操作。",
                        "导入被阻止", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查是否已存在
                var existing = await _wallAppService.GetDetailByWallIdAsync(wallId);
                if (existing != null)
                {
                    // 已存在且未审核 → 同步更新
                    var confirmResult = MessageBox.Show(
                        $"墙体 \"{wallId}\" 已存在（未审核），是否同步更新 BimData？\n\n更新后将替换 BimJson、清空 MomData、重置管线阶段和生产状态。",
                        "确认同步更新", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (confirmResult != MessageBoxResult.Yes)
                        return;

                    await _wallAppService.SyncBimDataAsync(existing.Id, jsonContent, schemaVer, wallName, importedBy);
                    MessageBox.Show($"墙体 \"{wallId}\" BimData 已同步更新。\n版本号: {schemaVer}",
                        "同步完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
					// 不存在 → 新建 + 创建项目批次
					var projectName = Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "单墙导入";
					var projectId = await _projectAppService.CreateProjectAsync(
                        projectName, Path.GetDirectoryName(filePath) ?? "", hostName, importedBy, 1);

                    var wall = new WallEntity(projectId, wallId, projectName, 1, jsonContent, wallName, schemaVer);
                    await _wallAppService.InsertManyAsync(new List<WallEntity> { wall });

                    MessageBox.Show($"墙体 \"{wallId}\" 已成功导入。\n版本号: {schemaVer}",
                        "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await ApplyFiltersAsync();
                await UpdateAvailableFloorsAsync();

                _logger.LogInformation("单墙导入完成: WallId={WallId}, File={FileName}", wallId, fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError(ex, "单墙导入异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ==================== 命令：执行管线（完整流程：校验Bim→转换→校验Mom） ====================
        [RelayCommand]
        private async Task ExecutePipelineAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要执行管线的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsLoading = true;
            ImportProgressMessage = "正在执行管线...";
            ImportProgressMax = SelectedItems.Count;
            ImportProgressValue = 0;

            int successCount = 0;
            int failCount = 0;
            var failedIds = new List<string>();

            try
            {
                foreach (var item in SelectedItems)
                {
                    ImportProgressValue++;
                    ImportProgressMessage = $"管线处理: {item.WallId} ({ImportProgressValue}/{ImportProgressMax})";

                    try
                    {
                        var result = await _pipelineService.ExecutePipelineAsync(item.Id);

                        if (result.FinalStage == PipelineStage.Ready)
                        {
                            successCount++;
                            item.PipelineStage = PipelineStage.Ready;
                            item.Status = ProcessStatus.待加工;
                        }
                        else
                        {
                            failCount++;
                            failedIds.Add(item.WallId);
                            item.PipelineStage = result.FinalStage;

                            if (result.Errors.Count > 0)
                            {
                                item.ValidationErrorSummary = string.Join("; ",
                                    result.Errors.Select(e => e.ErrorMessage));
                            }
                        }

                        if (result.MomJsonData != null)
                            item.MomJsonData = result.MomJsonData;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        failedIds.Add(item.WallId);
                        _logger.LogError(ex, "管线执行异常: {WallId}", item.WallId);
                    }
                }

                ImportProgressMessage = $"管线执行完成：成功 {successCount}，失败 {failCount}";

                if (failCount > 0)
                {
                    var failList = string.Join("\n", failedIds.Select(id => $"  • {id}"));
                    MessageBox.Show(
                        $"管线执行完成\n成功: {successCount}\n失败: {failCount}\n\n失败墙体:\n{failList}",
                        "管线结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await Task.Delay(2000);
                RefreshDisplay();
            }
            catch (Exception ex)
            {
                ImportProgressMessage = $"管线异常: {ex.Message}";
                _logger.LogError(ex, "批量管线异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ==================== 命令：批量 Bim→Mom 转换 ====================
        [RelayCommand]
        private async Task BatchConvertBimToMomAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要转换的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirmResult = MessageBox.Show(
                $"将对选中的 {SelectedItems.Count} 面墙体执行 Bim→Mom 转换管线。\n\n是否继续？",
                "确认批量转换", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            IsLoading = true;
            ImportProgressMessage = "正在批量转换 Bim→Mom...";
            ImportProgressMax = SelectedItems.Count;
            ImportProgressValue = 0;

            int successCount = 0;
            int failCount = 0;

            try
            {
                foreach (var item in SelectedItems)
                {
                    ImportProgressValue++;
                    ImportProgressMessage = $"转换: {item.WallId} ({ImportProgressValue}/{ImportProgressMax})";

                    try
                    {
                        var result = await _pipelineService.ExecutePipelineAsync(item.Id);
                        if (result.FinalStage == PipelineStage.Ready)
                        {
                            successCount++;
                            item.PipelineStage = PipelineStage.Ready;
                            item.Status = ProcessStatus.待加工;
                        }
                        else
                        {
                            failCount++;
                            item.PipelineStage = result.FinalStage;
                            if (result.Errors.Count > 0)
                                item.ValidationErrorSummary = string.Join("; ", result.Errors.Select(e => e.ErrorMessage));
                        }
                        if (result.MomJsonData != null)
                            item.MomJsonData = result.MomJsonData;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, "转换异常: {WallId}", item.WallId);
                    }
                }

                ImportProgressMessage = $"批量转换完成：成功 {successCount}，失败 {failCount}";
                MessageBox.Show($"批量转换完成\n成功: {successCount}\n失败: {failCount}",
                    "转换结果", MessageBoxButton.OK,
                    failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                await Task.Delay(2000);
                RefreshDisplay();
            }
            catch (Exception ex)
            {
                ImportProgressMessage = $"转换异常: {ex.Message}";
                _logger.LogError(ex, "批量转换异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ==================== 命令：审核 ====================
        [RelayCommand]
        private async Task AuditSelectedAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要审核的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确认审核选中的 {SelectedItems.Count} 面墙体？\n审核后将锁定数据，不可覆盖导入 BimData。",
                "确认审核", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            var updatedBy = Environment.UserName;
            var wallIds = SelectedItems.Select(i => i.Id).ToList();

            await _wallAppService.SetAuditStatusBatchAsync(wallIds, (int)AuditStatus.已审核, updatedBy);

            foreach (var item in SelectedItems)
            {
                item.AuditStatus = (int)AuditStatus.已审核;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            RefreshDisplay();
            _logger.LogInformation("批量审核: {Count}条", SelectedItems.Count);
        }

        // ==================== 命令：反审核 ====================
        [RelayCommand]
        private async Task UnauditSelectedAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要反审核的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确认反审核选中的 {SelectedItems.Count} 面墙体？\n反审核后可以重新导入 BimData 进行测试。",
                "确认反审核", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            var updatedBy = Environment.UserName;
            var wallIds = SelectedItems.Select(i => i.Id).ToList();

            await _wallAppService.SetAuditStatusBatchAsync(wallIds, (int)AuditStatus.未审核, updatedBy);

            foreach (var item in SelectedItems)
            {
                item.AuditStatus = (int)AuditStatus.未审核;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            RefreshDisplay();
            _logger.LogInformation("批量反审核: {Count}条", SelectedItems.Count);
        }

        // ==================== 命令：提高优先级（Priority+1） ====================
        [RelayCommand]
        private async Task IncreasePriorityAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要调整优先级的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var updatedBy = Environment.UserName;
            foreach (var item in SelectedItems)
            {
                var newPriority = item.Priority + 1;
                await _wallAppService.UpdatePrioritiesAsync(new List<long> { item.Id }, newPriority, updatedBy);
                item.Priority = newPriority;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            RefreshDisplay();
            _logger.LogInformation("提高优先级: {Count}条", SelectedItems.Count);
        }

        // ==================== 命令：降低优先级（Priority-1） ====================
        [RelayCommand]
        private async Task DecreasePriorityAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要调整优先级的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var updatedBy = Environment.UserName;
            foreach (var item in SelectedItems)
            {
                var newPriority = Math.Max(0, item.Priority - 1);
                await _wallAppService.UpdatePrioritiesAsync(new List<long> { item.Id }, newPriority, updatedBy);
                item.Priority = newPriority;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            RefreshDisplay();
            _logger.LogInformation("降低优先级: {Count}条", SelectedItems.Count);
        }

        // ==================== 命令：批量设置优先级 ====================
        [RelayCommand]
        private async Task ModifyPriorityAsync(ProcessPriority priority)
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要修改的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var updatedBy = Environment.UserName;
            var wallIds = SelectedItems.Select(i => i.Id).ToList();

            await _wallAppService.UpdatePrioritiesAsync(wallIds, (int)priority, updatedBy);

            foreach (var item in SelectedItems)
            {
                item.Priority = (int)priority;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            RefreshDisplay();
            _logger.LogInformation("批量修改优先级: {Count}条 → {Priority}", SelectedItems.Count, priority);
        }

        // ==================== 命令：修改生产状态 ====================
        [RelayCommand]
        private async Task ModifyStatusAsync(ProcessStatus status)
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要修改的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var updatedBy = Environment.UserName;
            var wallIds = SelectedItems.Select(i => i.Id).ToList();

            await _wallAppService.UpdateStatusesAsync(wallIds, (int)status, updatedBy);

            foreach (var item in SelectedItems)
            {
                item.Status = status;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            RefreshDisplay();
            _logger.LogInformation("批量修改状态: {Count}条 → {Status}", SelectedItems.Count, status);
        }

        // ==================== 命令：修改墙体名称 ====================
        [RelayCommand]
        private async Task RenameWallAsync(WallListItem? item)
        {
            if (item == null) return;

            var newName = InteractionHelper.ShowInputDialog(
                LocalizationService.Instance["Dlg_RenameWallTitle"],
                string.Format(LocalizationService.Instance["Dlg_RenameWallPrompt"], item.WallId),
                item.WallName);

            if (newName == null) // 用户取消
                return;

            var updatedBy = Environment.UserName;
            await _wallAppService.UpdateWallNameAsync(item.Id, newName, updatedBy);

            _logger.LogInformation("修改墙体名称: WallId={WallId}, NewName={WallName}", item.WallId, newName);

            // 立刻刷新表格数据
            await ApplyFiltersAsync();
        }

        // ==================== 命令：软删除 ====================
        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要删除的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确认软删除选中的 {SelectedItems.Count} 条墙体数据？\n\n（软删除后可通过\"显示已删除\"筛选找到并恢复）",
                "确认软删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            var updatedBy = Environment.UserName;
            var toRemove = SelectedItems.ToList();
            var wallIds = toRemove.Select(i => i.Id).ToList();

            await _wallAppService.SoftDeleteManyAsync(wallIds, updatedBy);

            foreach (var item in toRemove)
            {
                item.IsSelected = false;
                item.IsDeleted = true;
            }

            SelectedItems.Clear();
            await ApplyFiltersAsync();
            _logger.LogInformation("批量软删除: {Count}条", toRemove.Count);
        }

        // ==================== 命令：恢复已删除 ====================
        [RelayCommand]
        private async Task RestoreSelectedAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要恢复的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var updatedBy = Environment.UserName;
            var wallIds = SelectedItems.Select(i => i.Id).ToList();

            await _wallAppService.RestoreManyAsync(wallIds, updatedBy);

            foreach (var item in SelectedItems)
            {
                item.IsDeleted = false;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            await ApplyFiltersAsync();
            _logger.LogInformation("批量恢复: {Count}条", SelectedItems.Count);
        }

        // ==================== 命令：导出 ====================
        [RelayCommand]
        private async Task ExportAsync()
        {
            var dialog = new SaveFileDialog
            {
                Title = "导出墙体清单",
                Filter = "CSV 文件|*.csv|JSON 文件|*.json",
                FileName = $"墙体清单_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var data = _filteredItems;
                var ext = Path.GetExtension(dialog.FileName).ToLower();

                if (ext == ".csv")
                    await ExportCsvAsync(dialog.FileName, data);
                else
                    await ExportJsonAsync(dialog.FileName, data);

                MessageBox.Show($"成功导出 {data.Count} 条记录", "导出完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _logger.LogInformation("导出完成: {Path}, {Count}条", dialog.FileName, data.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError(ex, "导出失败");
            }
        }

        // ==================== 命令：查看BIM模型渲染 ====================
        [RelayCommand]
        private void ViewDetail(WallListItem? item)
        {
            if (item == null) return;

            _mainPageViewModel.AddOrActivateTab("BimDataRenderPage", onPageCreated: page =>
            {
                if (page is Views.BimDataRenderPage bimPage && bimPage.DataContext is BimDataRenderViewModel vm)
                {
                    _ = vm.SearchAndLoadAsync(item.WallId);
                }
            });

            _logger.LogInformation("跳转到BIM模型渲染: WallId={WallId}", item.WallId);
        }

        // ==================== 命令：查看MOM模型渲染 ====================
        [RelayCommand]
        private void ViewMomRender(WallListItem? item)
        {
            if (item == null) return;

            _mainPageViewModel.AddOrActivateTab("MomDataRenderPage", onPageCreated: page =>
            {
                if (page is Views.MomDataRenderPage momPage && momPage.DataContext is MomDataRenderViewModel vm)
                {
                    _ = vm.SearchAndLoadAsync(item.WallId);
                }
            });

            _logger.LogInformation("跳转到MOM模型渲染: WallId={WallId}", item.WallId);
        }

        // ==================== 命令：编辑 JSON 数据（跳转到 JSON 编辑器） ====================
        [RelayCommand]
        private void EditJsonData(WallListItem? item)
        {
            if (item == null) return;

            _mainPageViewModel.AddOrActivateTab("JsonEditPage", onPageCreated: page =>
            {
                if (page is Views.JsonEditPage jsonPage && jsonPage.DataContext is JsonEditPageViewModel vm)
                    _ = vm.SetWallIdAsync(item.WallId);
            });

            _logger.LogInformation("跳转到JSON编辑器: WallId={WallId}", item.WallId);
        }

        // ==================== 命令：搜索 ====================
        [RelayCommand]
        private async Task SearchAsync()
        {
            CurrentPage = 1;
            await ApplyFiltersAsync();
        }

        // ==================== 命令：重置筛选 ====================
        [RelayCommand]
        private async Task ResetFilterAsync()
        {
            SearchProjectName = string.Empty;
            SearchWallId = string.Empty;
            SearchWallName = string.Empty;
            FilterFloor = null;
            FilterStatus = null;
            FilterPriority = null;
            FilterPipelineStage = null;
			FilterAuditStatus = null;
			FilterDateFrom = null;
            FilterDateTo = null;
            CurrentPage = 1;
            await ApplyFiltersAsync();
        }

        // ==================== 命令：翻页 ====================
        [RelayCommand]
        private async Task GoToPage(object? parameter)
        {
            var page = parameter switch
            {
                int p => p,
                string s when int.TryParse(s, out var p) => p,
                _ => -1
            };

            if (page < 1 || page > TotalPages) return;
            CurrentPage = page;
            await ApplyFiltersAsync();
        }

        // ==================== 命令：修改每页条数 ====================
        [RelayCommand]
        private async Task ChangePageSizeAsync(object? parameter)
        {
            var size = parameter switch
            {
                int s => s,
                string str when int.TryParse(str, out var s) => s,
                _ => 20
            };

            PageSize = size;
            CurrentPage = 1;
            await ApplyFiltersAsync();
        }

        // ==================== 命令：排序 ====================
        [RelayCommand]
        private async Task SortByAsync(string field)
        {
            if (SortField == field)
                SortAscending = !SortAscending;
            else
            {
                SortField = field;
                SortAscending = true;
            }

            await ApplyFiltersAsync();
        }

        // ==================== 核心方法：应用筛选 ====================
        public async Task ApplyFiltersAsync()
        {
            try
            {
                var input = new WallQueryInput
                {
                    ProjectName = string.IsNullOrWhiteSpace(SearchProjectName) ? null : SearchProjectName,
                    Floor = FilterFloor,
                    WallId = string.IsNullOrWhiteSpace(SearchWallId) ? null : SearchWallId,
                    WallName = string.IsNullOrWhiteSpace(SearchWallName) ? null : SearchWallName,
                    Statuses = FilterStatus.HasValue
                        ? new List<int> { (int)FilterStatus.Value }
                        : null,
                    Priorities = FilterPriority.HasValue
                        ? new List<int> { (int)FilterPriority.Value }
                        : null,
                    PipelineStages = FilterPipelineStage.HasValue
                        ? new List<PipelineStage> { FilterPipelineStage.Value }
                        : null,
                    AuditStatuses = FilterAuditStatus.HasValue
                        ? new List<int> { (int)FilterAuditStatus.Value }
                        : null,
                    EndProductionTimeFrom = FilterDateFrom,
                    EndProductionTimeTo = FilterDateTo,
                    SortField = SortField,
                    SortAscending = SortAscending,
                    Page = CurrentPage,
                    PageSize = PageSize
                };

                var pagedResult = await _wallAppService.QueryWallsAsync(input);

                var entities = pagedResult.Items;
                _filteredItems = MapDtosToWallListItems(entities);

                TotalItems = (int)pagedResult.TotalCount;
                TotalPages = TotalItems > 0
                    ? (int)Math.Ceiling((double)TotalItems / PageSize)
                    : 0;

                if (TotalPages == 0)
                {
                    CurrentPage = 1;
                }
                else if (CurrentPage > TotalPages)
                {
                    CurrentPage = TotalPages;
                }

                HasPreviousPage = TotalPages > 0 && CurrentPage > 1;
                HasNextPage = TotalPages > 0 && CurrentPage < TotalPages;
                IsEmpty = TotalItems == 0;

                RefreshDisplay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "应用筛选失败");
                HasError = true;
                ErrorMessage = $"查询失败: {ex.Message}";
            }
        }

        // ==================== 刷新当前页数据 ====================
        private void RefreshDisplay()
        {
            DisplayItems = new ObservableCollection<WallListItem>(_filteredItems);
            UpdateIsAllSelected();
        }

        // ==================== DTO → WallListItem 映射 ====================
		private static List<WallListItem> MapDtosToWallListItems(List<WallDto> dtos)
		{
			return dtos.Select(dto => new WallListItem
			{
				Id = dto.Id,
				ProjectName = dto.ProjectName,
				Floor = dto.Floor,
				WallId = dto.WallId,
				WallName = dto.WallName,
				ImportTime = dto.ImportTime,
				StartProductionTime = dto.StartProductionTime,
				EndProductionTime = dto.EndProductionTime,
				PipelineStage = dto.PipelineStage,
				Priority = dto.Priority,
				Status = (ProcessStatus)dto.Status,
				AuditStatus = dto.AuditStatus,
				SchemaVersion = dto.SchemaVersion,
				IsDeleted = dto.IsDeleted,
				UpdatedAt = dto.UpdatedAt,
				UpdatedBy = dto.UpdatedBy,
				ValidationErrorSummary = dto.ValidationErrorSummary
			}).ToList();
		}

        // ==================== CheckBox 选中同步 ====================
        public void SyncSelectedItemsAndAllSelected()
        {
            var allItems = GetAllCurrentDisplayedItems();
            var selected = allItems.Where(x => x.IsSelected).ToList();
            SelectedItems = new ObservableCollection<WallListItem>(selected);
            UpdateIsAllSelected();
        }

        private List<WallListItem> GetAllCurrentDisplayedItems()
        {
            return _filteredItems.ToList();
        }

        private void UpdateIsAllSelected()
        {
            var currentPage = DisplayItems.ToList();
            if (currentPage.Count == 0)
            {
                IsAllSelected = false;
                return;
            }

            var selectedCount = currentPage.Count(x => x.IsSelected);
            IsAllSelected = selectedCount == currentPage.Count
                ? true
                : selectedCount == 0
                    ? false
                    : null;
        }

        public void SelectAllCurrentPage()
        {
            foreach (var item in DisplayItems)
                item.IsSelected = true;
            SyncSelectedItemsAndAllSelected();
        }

        public void DeselectAllCurrentPage()
        {
            foreach (var item in DisplayItems)
                item.IsSelected = false;
            SyncSelectedItemsAndAllSelected();
        }

        // ==================== 更新可选楼层列表 ====================
        private async Task UpdateAvailableFloorsAsync()
        {
            var floors = await _wallAppService.GetAvailableFloorsAsync(
                string.IsNullOrWhiteSpace(SearchProjectName) ? null : SearchProjectName);
            AvailableFloors = new ObservableCollection<int>(floors);
        }

        // ==================== 从 BimJson 提取墙体名称 ====================
        private static string ExtractWallName(string jsonContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var nameEl))
                    return nameEl.GetString() ?? string.Empty;
                if (root.TryGetProperty("wallName", out var wallNameEl))
                    return wallNameEl.GetString() ?? string.Empty;
                if (root.TryGetProperty("houseNumber", out var houseEl))
                    return houseEl.GetString() ?? string.Empty;
            }
            catch { }
            return string.Empty;
        }

        // ==================== 判断是否为 LGS Wall ====================
        /// <summary>
        /// 判断 JSON 内容对应的墙体是否为 LGS Wall（轻钢龙骨墙体）。
        /// LGS Wall 判断条件：JSON 根对象中含有 "tracks" 或 "nogs" 字段。
        /// </summary>
        private static bool IsLgsWall(string jsonContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;
                return root.TryGetProperty("tracks", out _) || root.TryGetProperty("nogs", out _);
            }
            catch
            {
                return false;
            }
        }

        // ==================== 导出 CSV ====================
        private static async Task ExportCsvAsync(string filePath, List<WallListItem> data)
        {
            await using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync("项目名称,楼层,墙体ID,墙体名称,版本号,导入时间,管线阶段,加工优先级,生产状态,审核状态");

            foreach (var item in data)
            {
                await writer.WriteLineAsync(
                    $"\"{item.ProjectName}\",{item.Floor},\"{item.WallId}\",\"{item.WallName}\"," +
                    $"{item.SchemaVersion}," +
                    $"\"{item.ImportTime:yyyy-MM-dd HH:mm:ss}\",{item.PipelineStageText},{item.Priority},{item.Status},{item.AuditStatusText}");
            }
        }

        // ==================== 导出 JSON ====================
        private static async Task ExportJsonAsync(string filePath, List<WallListItem> data)
        {
            var exportList = data.Select(x => new
            {
                x.ProjectName,
                x.Floor,
                x.WallId,
                x.WallName,
                Version = x.SchemaVersion,
                ImportTime = x.ImportTime.ToString("yyyy-MM-dd HH:mm:ss"),
                PipelineStage = x.PipelineStageText,
                Priority = x.Priority,
                Status = x.Status.ToString(),
                AuditStatus = x.AuditStatusText,
                x.ValidationErrorSummary
            }).ToList();

            var json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
        }
    }

    /// <summary>
    /// 简单的输入对话框辅助类
    /// </summary>
    internal static class InteractionHelper
    {
        public static string? ShowInputDialog(string title, string message, string defaultValue = "")
        {
            var result = Microsoft.VisualBasic.Interaction.InputBox(
                message, title, defaultValue, -1, -1);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
    }
}
