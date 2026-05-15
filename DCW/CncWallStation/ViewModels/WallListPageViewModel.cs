using CncWallStation.Models;
using CncWallStation.Models.Enums;
using CncWallStation.Repositories;
using CncWallStation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private readonly IWallRepository _wallRepo;
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

        /// <summary>列头全选状态：true=全选, false=全不选, null=部分选中</summary>
        [ObservableProperty]
        private bool? _isAllSelected = false;

        // ==================== 分页按钮状态 ====================
        [ObservableProperty]
        private bool _hasPreviousPage;

        [ObservableProperty]
        private bool _hasNextPage;

        // ==================== 筛选属性 ====================
        [ObservableProperty]
        private string _searchHouseNumber = string.Empty;

        [ObservableProperty]
        private string _searchWallId = string.Empty;

        [ObservableProperty]
        private int? _filterFloor;

        [ObservableProperty]
        private ObservableCollection<int> _availableFloors = new();

        [ObservableProperty]
        private ObservableCollection<ProcessStatus> _selectedStatuses = new();

        [ObservableProperty]
        private ObservableCollection<ProcessPriority> _selectedPriorities = new();

        [ObservableProperty]
        private ObservableCollection<PipelineStage> _selectedPipelineStages = new();

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
        private string _sortField = nameof(WallListItem.ImportTime);

        [ObservableProperty]
        private bool _sortAscending;

        // ==================== 可用选项列表（用于下拉筛选） ====================
        public List<ProcessStatus> AllStatuses { get; } = new()
        { ProcessStatus.待加工, ProcessStatus.加工中, ProcessStatus.已完成, ProcessStatus.异常 };

        public List<ProcessPriority> AllPriorities { get; } = new()
        { ProcessPriority.高, ProcessPriority.中, ProcessPriority.低 };

        public List<PipelineStage> AllPipelineStages { get; } = new()
        {
            PipelineStage.Imported, PipelineStage.BimInvalid,
            PipelineStage.ConversionFailed, PipelineStage.MomInvalid,
            PipelineStage.Ready
        };

        public List<int> PageSizeOptions { get; } = new() { 10, 20, 50, 100 };

        // ==================== 构造函数 ====================
        public WallListPageViewModel(
            ILogger<WallListPageViewModel> logger,
            IWallRepository wallRepo,
            IPipelineService pipelineService)
        {
            _logger = logger;
            _wallRepo = wallRepo;
            _pipelineService = pipelineService;

            // 启动时从数据库加载数据
            _ = LoadDataFromDbAsync();
        }

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
                var projectNumber = Path.GetFileName(rootFolder);

                // 扫描数字命名的楼层子文件夹
                var floorFolders = new List<(int FloorIndex, string FolderPath)>();
                foreach (var subDir in Directory.GetDirectories(rootFolder))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (int.TryParse(dirName, out var floorIndex))
                    {
                        floorFolders.Add((floorIndex, subDir));
                    }
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
                int successCount = 0;
                int failCount = 0;
                var hostName = Environment.MachineName;
                var importedBy = Environment.UserName;

                // 归档旧版本
                await _wallRepo.ArchiveOldVersionsAsync(projectNumber);

                // 创建新的导入批次
                var projectId = await _wallRepo.CreateProjectAsync(
                    projectNumber, rootFolder, hostName, importedBy, fileEntries.Count);

                var walls = new List<Models.Entities.WallEntity>();

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

                        walls.Add(new Models.Entities.WallEntity
                        {
                            ProjectId = projectId,
                            WallId = wallId,
                            ProjectNumber = projectNumber,
                            Floor = floor,
                            BimJsonData = jsonContent,
                            PipelineStage = PipelineStage.Imported,
                            Priority = (int)MapFloorToPriority(floor),
                            Status = 0,
                            ImportTime = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = importedBy
                        });

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, "读取文件失败: {FilePath}", filePath);
                    }

                    if (i % 10 == 0)
                        await Task.Delay(1);
                }

                // 批量写入数据库
                if (walls.Count > 0)
                {
                    await _wallRepo.AddWallsAsync(walls);
                }

                ImportProgressMessage = $"导入完成：成功 {successCount}，失败 {failCount}";
                await Task.Delay(2000);

                await ApplyFiltersAsync();
                await UpdateAvailableFloorsAsync();

                _logger.LogInformation("批量导入完成: 项目={Project}, 成功{Success}, 失败{Fail}",
                    projectNumber, successCount, failCount);
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

        // ==================== 楼层 → 优先级映射 ====================
        private static ProcessPriority MapFloorToPriority(int floor) => floor switch
        {
            1 => ProcessPriority.高,
            2 => ProcessPriority.中,
            _ => ProcessPriority.低
        };

        // ==================== 命令：执行管线 ====================
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
                        "管线结果", MessageBoxButton.OK,
                        failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
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

        // ==================== 命令：修改优先级 ====================
        [RelayCommand]
        private async Task ModifyPriorityAsync(ProcessPriority priority)
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要修改的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var updatedBy = Environment.UserName;
            foreach (var item in SelectedItems)
            {
                await _wallRepo.UpdatePriorityAsync(item.Id, (int)priority, updatedBy);
                item.Priority = priority;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            RefreshDisplay();
            _logger.LogInformation("批量修改优先级: {Count}条 → {Priority}", SelectedItems.Count, priority);
        }

        // ==================== 命令：修改状态 ====================
        [RelayCommand]
        private async Task ModifyStatusAsync(ProcessStatus status)
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要修改的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var updatedBy = Environment.UserName;
            foreach (var item in SelectedItems)
            {
                await _wallRepo.UpdateStatusAsync(item.Id, (int)status, updatedBy);
                item.Status = status;
                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;
            }

            RefreshDisplay();
            _logger.LogInformation("批量修改状态: {Count}条 → {Status}", SelectedItems.Count, status);
        }

        // ==================== 命令：删除 ====================
        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要删除的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确认删除选中的 {SelectedItems.Count} 条墙体数据？\n此操作不可撤销。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            var toRemove = SelectedItems.ToList();
            foreach (var item in toRemove)
            {
                item.IsSelected = false;
                await _wallRepo.DeleteWallAsync(item.Id);
                DisplayItems.Remove(item);
            }

            SelectedItems.Clear();
            await ApplyFiltersAsync();
            _logger.LogInformation("批量删除: {Count}条", toRemove.Count);
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
                {
                    await ExportCsvAsync(dialog.FileName, data);
                }
                else
                {
                    await ExportJsonAsync(dialog.FileName, data);
                }

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

        // ==================== 命令：查看详情 ====================
        [RelayCommand]
        private void ViewDetail(WallListItem? item)
        {
            if (item == null) return;

            try
            {
                var data = item.MomJsonData ?? item.MjsonData;
                var formatted = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<object>(data),
                    new JsonSerializerOptions { WriteIndented = true });

                var title = item.MomJsonData != null
                    ? $"墙体详情 (MomJSON) - {item.WallId}"
                    : $"墙体详情 (BimJSON) - {item.WallId}";

                var message = formatted;
                if (item.ValidationErrorSummary != null)
                    message += $"\n\n--- 校验失败原因 ---\n{item.ValidationErrorSummary}";
                if (item.PipelineStage != PipelineStage.Imported && item.PipelineStage != PipelineStage.Ready)
                    message += $"\n\n管线阶段: {item.PipelineStageText}";

                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show(item.MomJsonData ?? item.MjsonData,
                    $"墙体详情 - {item.WallId}",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ==================== 命令：编辑 JSON 数据 ====================
        [RelayCommand]
        private async Task EditJsonDataAsync(WallListItem? item)
        {
            if (item == null) return;

            // 仅异常状态允许编辑
            if (item.PipelineStage != PipelineStage.BimInvalid &&
                item.PipelineStage != PipelineStage.ConversionFailed &&
                item.PipelineStage != PipelineStage.MomInvalid)
            {
                MessageBox.Show("仅异常状态（BimInvalid/ConversionFailed/MomInvalid）的墙体支持编辑 JSON 数据。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 确定编辑哪个 JSON
            bool editMom = item.PipelineStage == PipelineStage.MomInvalid ||
                           item.PipelineStage == PipelineStage.ConversionFailed;

            var currentJson = editMom ? item.MomJsonData ?? "" : item.MjsonData;
            var title = editMom ? "编辑 MomJSON 数据" : "编辑 BimJSON 数据";

            // 简化版：用 InputBox 方式编辑（实际可用专门的 JSON 编辑窗口）
            var inputWindow = new Window
            {
                Title = $"{title} - {item.WallId}",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new System.Windows.Controls.TextBox
                {
                    Text = currentJson,
                    AcceptsReturn = true,
                    AcceptsTab = true,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 13,
                    Margin = new System.Windows.Thickness(10)
                }
            };

            inputWindow.ShowDialog();

            if (inputWindow.Content is System.Windows.Controls.TextBox textBox &&
                textBox.Text != currentJson)
            {
                var updatedBy = Environment.UserName;
                string? newBim = null;
                string? newMom = null;

                if (editMom)
                    newMom = textBox.Text;
                else
                    newBim = textBox.Text;

                await _wallRepo.UpdateJsonDataAsync(item.Id, newBim, newMom, updatedBy);

                if (editMom)
                    item.MomJsonData = newMom;
                else
                    item.MjsonData = newBim!;

                item.UpdatedBy = updatedBy;
                item.UpdatedAt = DateTime.Now;

                MessageBox.Show("JSON 数据已保存，可重新触发管线。", "保存成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                _logger.LogInformation("手动编辑 JSON: {WallId}, 类型={Type}, 修改者={User}",
                    item.WallId, editMom ? "MomJSON" : "BimJSON", updatedBy);
            }
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
            SearchHouseNumber = string.Empty;
            SearchWallId = string.Empty;
            FilterFloor = null;
            SelectedStatuses.Clear();
            SelectedPriorities.Clear();
            SelectedPipelineStages.Clear();
            FilterDateFrom = null;
            FilterDateTo = null;
            CurrentPage = 1;
            await ApplyFiltersAsync();
        }

        // ==================== 命令：翻页 ====================
        [RelayCommand]
        private void GoToPage(object? parameter)
        {
            var page = parameter switch
            {
                int p => p,
                string s when int.TryParse(s, out var p) => p,
                _ => -1
            };

            if (page < 1 || page > TotalPages) return;
            CurrentPage = page;
            HasPreviousPage = CurrentPage > 1;
            HasNextPage = CurrentPage < TotalPages;
            RefreshDisplay();
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
                var pipelineStages = SelectedPipelineStages.Any()
                    ? SelectedPipelineStages.ToList()
                    : null;

                var statuses = SelectedStatuses.Any()
                    ? SelectedStatuses.Select(s => (int)s).ToList()
                    : null;

                var priorities = SelectedPriorities.Any()
                    ? SelectedPriorities.Select(p => (int)p).ToList()
                    : null;

                var (items, totalCount) = await _wallRepo.QueryWallsAsync(
                    projectNumber: string.IsNullOrWhiteSpace(SearchHouseNumber) ? null : SearchHouseNumber,
                    floor: FilterFloor,
                    wallId: string.IsNullOrWhiteSpace(SearchWallId) ? null : SearchWallId,
                    statuses: statuses,
                    priorities: priorities,
                    pipelineStages: pipelineStages,
                    importTimeFrom: FilterDateFrom,
                    importTimeTo: FilterDateTo,
                    sortField: SortField,
                    sortAscending: SortAscending,
                    page: CurrentPage,
                    pageSize: PageSize,
                    latestOnly: true);

                // 映射 Entity → Display Model
                _filteredItems = items.Select(MapToWallListItem).ToList();
                TotalItems = totalCount;
                TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);

                if (CurrentPage > TotalPages)
                    CurrentPage = Math.Max(1, TotalPages);

                HasPreviousPage = CurrentPage > 1;
                HasNextPage = CurrentPage < TotalPages;
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
            var pageItems = _filteredItems
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            DisplayItems = new ObservableCollection<WallListItem>(pageItems);
            UpdateIsAllSelected();
        }

        // ==================== Entity → WallListItem 映射 ====================
        private static WallListItem MapToWallListItem(Models.Entities.WallEntity entity)
        {
            var item = new WallListItem
            {
                Id = entity.Id,
                HouseNumber = entity.ProjectNumber,
                Floor = entity.Floor,
                WallId = entity.WallId,
                ImportTime = entity.ImportTime,
                MjsonData = entity.BimJsonData,
                MomJsonData = entity.MomJsonData,
                PipelineStage = entity.PipelineStage,
                Priority = (ProcessPriority)entity.Priority,
                Status = (ProcessStatus)entity.Status,
                UpdatedAt = entity.UpdatedAt,
                UpdatedBy = entity.UpdatedBy
            };

            // 汇总校验失败原因
            if (entity.ValidationErrors?.Count > 0)
            {
                item.ValidationErrorSummary = string.Join("; ",
                    entity.ValidationErrors
                        .OrderByDescending(e => e.CreatedAt)
                        .Select(e => e.ErrorMessage));
            }

            return item;
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
            var floors = await _wallRepo.GetAvailableFloorsAsync(
                string.IsNullOrWhiteSpace(SearchHouseNumber) ? null : SearchHouseNumber);
            AvailableFloors = new ObservableCollection<int>(floors);
        }

        // ==================== 导出 CSV ====================
        private static async Task ExportCsvAsync(string filePath, List<WallListItem> data)
        {
            await using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync("房屋编号,楼层,墙体ID,导入时间,管线阶段,加工优先级,加工状态");

            foreach (var item in data)
            {
                await writer.WriteLineAsync(
                    $"\"{item.HouseNumber}\",{item.Floor},\"{item.WallId}\"," +
                    $"\"{item.ImportTime:yyyy-MM-dd HH:mm:ss}\",{item.PipelineStageText},{item.Priority},{item.Status}");
            }
        }

        // ==================== 导出 JSON ====================
        private static async Task ExportJsonAsync(string filePath, List<WallListItem> data)
        {
            var exportList = data.Select(x => new
            {
                x.HouseNumber,
                x.Floor,
                x.WallId,
                ImportTime = x.ImportTime.ToString("yyyy-MM-dd HH:mm:ss"),
                PipelineStage = x.PipelineStageText,
                Priority = x.Priority.ToString(),
                Status = x.Status.ToString(),
                x.ValidationErrorSummary
            }).ToList();

            var json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
        }
    }
}
