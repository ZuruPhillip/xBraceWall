using CncWallStation.Models;
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

        // ==================== 全量数据 ====================
        private List<WallListItem> _allItems = new();

        // 筛选后的数据副本
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

        public List<int> PageSizeOptions { get; } = new() { 10, 20, 50, 100 };

        // ==================== 构造函数 ====================
        public WallListPageViewModel(ILogger<WallListPageViewModel> logger)
        {
            _logger = logger;

            LoadMockData();
            ApplyFilters();
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
                var houseNumber = Path.GetFileName(rootFolder);

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
                var fileEntries = new List<(string FilePath, int FloorIndex)>();
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
                        fileEntries.Add((file, floorIndex));
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
                var results = new List<WallImportResult>();
                int successCount = 0;
                int failCount = 0;
                int duplicateCount = 0;
                var duplicates = new List<WallImportResult>();

                for (int i = 0; i < fileEntries.Count; i++)
                {
                    var (filePath, floorIndex) = fileEntries[i];
                    var fileName = Path.GetFileName(filePath);
                    var wallId = Path.GetFileNameWithoutExtension(filePath);
                    var floor = floorIndex + 1; // 文件夹 "0" 对应 1 楼

                    ImportProgressValue = i + 1;
                    ImportProgressMessage = $"正在处理: {fileName} ({i + 1}/{fileEntries.Count})";

                    try
                    {
                        var jsonContent = await File.ReadAllTextAsync(filePath);

                        var item = new WallListItem
                        {
                            HouseNumber = houseNumber,
                            Floor = floor,
                            WallId = wallId,
                            MjsonData = jsonContent,
                            ImportTime = DateTime.Now,
                            Status = ProcessStatus.待加工,
                            Priority = MapFloorToPriority(floor)
                        };

                        // 去重检查
                        var existing = _allItems.FirstOrDefault(x => x.WallId == item.WallId);
                        if (existing != null)
                        {
                            duplicateCount++;
                            duplicates.Add(new WallImportResult
                            {
                                FilePath = filePath,
                                FileName = fileName,
                                Success = true,
                                IsDuplicate = true,
                                Item = item,
                                Message = $"墙体ID '{item.WallId}' 已存在"
                            });
                            continue;
                        }

                        _allItems.Add(item);
                        successCount++;
                        results.Add(new WallImportResult
                        {
                            FilePath = filePath,
                            FileName = fileName,
                            Success = true,
                            Item = item
                        });
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        results.Add(new WallImportResult
                        {
                            FilePath = filePath,
                            FileName = fileName,
                            Success = false,
                            Message = $"处理失败: {ex.Message}"
                        });
                    }

                    // 每处理10个文件让出UI线程
                    if (i % 10 == 0)
                        await Task.Delay(1);
                }

                // 处理重复数据：询问用户是否覆盖
                if (duplicates.Any())
                {
                    var dupMsg = $"发现 {duplicates.Count} 个重复墙体ID:\n" +
                        string.Join("\n", duplicates.Select(d => $"  • {d.Item?.WallId} ({d.FileName})"));

                    dupMsg += "\n\n是否覆盖已有数据？";
                    var overwriteResult = MessageBox.Show(
                        dupMsg,
                        "重复数据确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (overwriteResult == MessageBoxResult.Yes)
                    {
                        foreach (var dup in duplicates.Where(d => d.Item != null))
                        {
                            var existing = _allItems.FirstOrDefault(x => x.WallId == dup.Item!.WallId);
                            if (existing != null)
                            {
                                var idx = _allItems.IndexOf(existing);
                                _allItems[idx] = dup.Item!;
                                successCount++;
                            }
                        }
                        duplicateCount = 0;
                    }
                }

                ImportProgressMessage = $"导入完成：成功 {successCount}，失败 {failCount}，重复 {duplicateCount}";
                await Task.Delay(2000);
                InitCascadeData();
                ApplyFilters();

                _logger.LogInformation("批量导入完成: 房屋={House}, 成功{Success}, 失败{Fail}, 重复{Dup}",
                    houseNumber, successCount, failCount, duplicateCount);
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
        /// <summary>
        /// 根据楼层映射加工优先级：1楼为高，2楼为中，3楼及以上为低
        /// </summary>
        private static ProcessPriority MapFloorToPriority(int floor) => floor switch
        {
            1 => ProcessPriority.高,
            2 => ProcessPriority.中,
            _ => ProcessPriority.低
        };

        // ==================== 初始化级联筛选数据 ====================
        /// <summary>
        /// 导入完成后初始化级联筛选等辅助数据
        /// </summary>
        private void InitCascadeData()
        {
            UpdateAvailableFloors();
        }

        // ==================== 命令：修改优先级 ====================
        [RelayCommand]
        private void ModifyPriority(ProcessPriority priority)
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要修改的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in SelectedItems)
                item.Priority = priority;

            RefreshDisplay();
            _logger.LogInformation("批量修改优先级: {Count}条 → {Priority}", SelectedItems.Count, priority);
        }

        // ==================== 命令：修改状态 ====================
        [RelayCommand]
        private void ModifyStatus(ProcessStatus status)
        {
            if (!SelectedItems.Any())
            {
                MessageBox.Show("请先选择要修改的墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in SelectedItems)
                item.Status = status;

            RefreshDisplay();
            _logger.LogInformation("批量修改状态: {Count}条 → {Status}", SelectedItems.Count, status);
        }

        // ==================== 命令：删除 ====================
        [RelayCommand]
        private void DeleteSelected()
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
                _allItems.Remove(item);
                DisplayItems.Remove(item);
            }

            SelectedItems.Clear();
            ApplyFilters();
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
                var formatted = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<object>(item.MjsonData),
                    new JsonSerializerOptions { WriteIndented = true });

                MessageBox.Show(formatted,
                    $"墙体详情 - {item.WallId}",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show(item.MjsonData,
                    $"墙体详情 - {item.WallId}",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ==================== 命令：搜索 ====================
        [RelayCommand]
        private void Search()
        {
            CurrentPage = 1;
            ApplyFilters();
        }

        // ==================== 命令：重置筛选 ====================
        [RelayCommand]
        private void ResetFilter()
        {
            SearchHouseNumber = string.Empty;
            SearchWallId = string.Empty;
            FilterFloor = null;
            SelectedStatuses.Clear();
            SelectedPriorities.Clear();
            FilterDateFrom = null;
            FilterDateTo = null;
            CurrentPage = 1;
            ApplyFilters();
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
        private void ChangePageSize(object? parameter)
        {
            var size = parameter switch
            {
                int s => s,
                string str when int.TryParse(str, out var s) => s,
                _ => 20
            };

            PageSize = size;
            CurrentPage = 1;
            ApplyFilters();
        }

        // ==================== 命令：排序 ====================
        [RelayCommand]
        private void SortBy(string field)
        {
            if (SortField == field)
                SortAscending = !SortAscending;
            else
            {
                SortField = field;
                SortAscending = true;
            }

            ApplyFilters();
        }

        // ==================== 核心方法：应用筛选 ====================
        public void ApplyFilters()
        {
            var query = _allItems.AsEnumerable();

            // 普通搜索筛选
            if (!string.IsNullOrWhiteSpace(SearchHouseNumber))
                query = query.Where(x => x.HouseNumber.Contains(SearchHouseNumber, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchWallId))
                query = query.Where(x => x.WallId.Contains(SearchWallId, StringComparison.OrdinalIgnoreCase));

            if (FilterFloor.HasValue)
                query = query.Where(x => x.Floor == FilterFloor.Value);

            if (SelectedStatuses.Any())
                query = query.Where(x => SelectedStatuses.Contains(x.Status));

            if (SelectedPriorities.Any())
                query = query.Where(x => SelectedPriorities.Contains(x.Priority));

            if (FilterDateFrom.HasValue)
                query = query.Where(x => x.ImportTime >= FilterDateFrom.Value);

            if (FilterDateTo.HasValue)
                query = query.Where(x => x.ImportTime <= FilterDateTo.Value.AddDays(1));

            // 排序
            query = SortField switch
            {
                nameof(WallListItem.HouseNumber) => SortAscending
                    ? query.OrderBy(x => x.HouseNumber)
                    : query.OrderByDescending(x => x.HouseNumber),
                nameof(WallListItem.Floor) => SortAscending
                    ? query.OrderBy(x => x.Floor)
                    : query.OrderByDescending(x => x.Floor),
                nameof(WallListItem.WallId) => SortAscending
                    ? query.OrderBy(x => x.WallId)
                    : query.OrderByDescending(x => x.WallId),
                nameof(WallListItem.Priority) => SortAscending
                    ? query.OrderBy(x => x.Priority)
                    : query.OrderByDescending(x => x.Priority),
                nameof(WallListItem.Status) => SortAscending
                    ? query.OrderBy(x => x.Status)
                    : query.OrderByDescending(x => x.Status),
                _ => SortAscending
                    ? query.OrderBy(x => x.ImportTime)
                    : query.OrderByDescending(x => x.ImportTime)
            };

            _filteredItems = query.ToList();
            TotalItems = _filteredItems.Count;
            TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
            if (CurrentPage > TotalPages)
                CurrentPage = Math.Max(1, TotalPages);

            HasPreviousPage = CurrentPage > 1;
            HasNextPage = CurrentPage < TotalPages;

            IsEmpty = TotalItems == 0 && _allItems.Count > 0;

            RefreshDisplay();
            UpdateAvailableFloors();
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

        // ==================== CheckBox 选中同步 ====================

        /// <summary>
        /// 从 IsSelected 属性重建 SelectedItems 集合，并刷新全选状态
        /// （行 CheckBox 勾选/取消后由 code-behind 调用）
        /// </summary>
        public void SyncSelectedItemsAndAllSelected()
        {
            var selected = _allItems.Where(x => x.IsSelected).ToList();
            SelectedItems = new ObservableCollection<WallListItem>(selected);
            UpdateIsAllSelected();
        }

        /// <summary>根据当前页 DisplayItems 的 IsSelected 计算全选状态</summary>
        private void UpdateIsAllSelected()
        {
            var currentPage = DisplayItems.ToList();
            if (currentPage.Count == 0)
            {
                IsAllSelected = false;
                return;
            }

            var selectedCount = currentPage.Count(x => x.IsSelected);
            IsAllSelected = selectedCount == currentPage.Count ? true
                          : selectedCount == 0 ? false
                          : null;
        }

        /// <summary>全选当前页（列头 CheckBox 勾选时调用）</summary>
        public void SelectAllCurrentPage()
        {
            foreach (var item in DisplayItems)
                item.IsSelected = true;
            SyncSelectedItemsAndAllSelected();
        }

        /// <summary>取消全选当前页（列头 CheckBox 取消时调用）</summary>
        public void DeselectAllCurrentPage()
        {
            foreach (var item in DisplayItems)
                item.IsSelected = false;
            SyncSelectedItemsAndAllSelected();
        }

        // ==================== 更新可选楼层列表 ====================
        private void UpdateAvailableFloors()
        {
            var source = string.IsNullOrWhiteSpace(SearchHouseNumber) ? _allItems
                : _allItems.Where(x => x.HouseNumber.Contains(SearchHouseNumber, StringComparison.OrdinalIgnoreCase));

            var floors = source.Select(x => x.Floor).Distinct().OrderBy(f => f).ToList();
            AvailableFloors = new ObservableCollection<int>(floors);
        }

        // ==================== 解析 .mjson 文件 ====================
        private WallListItem? ParseMjson(string filePath, string jsonContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                var wallId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty
                    : root.TryGetProperty("wallId", out var widProp) ? widProp.GetString() ?? string.Empty
                    : root.TryGetProperty("wall_id", out var wid2Prop) ? wid2Prop.GetString() ?? string.Empty
                    : Guid.NewGuid().ToString("N")[..8];

                var houseNumber = root.TryGetProperty("houseNumber", out var hnProp) ? hnProp.GetString() ?? "未知"
                    : root.TryGetProperty("house_number", out var hn2Prop) ? hn2Prop.GetString() ?? "未知"
                    : root.TryGetProperty("buildingId", out var bdProp) ? bdProp.GetString() ?? "未知"
                    : "未知";

                var floor = 1;
                if (root.TryGetProperty("floor", out var flProp) && flProp.TryGetInt32(out var f))
                    floor = f;
                else if (root.TryGetProperty("floorNumber", out var fl2Prop) && fl2Prop.TryGetInt32(out var f2))
                    floor = f2;

                return new WallListItem
                {
                    HouseNumber = houseNumber,
                    Floor = floor,
                    WallId = wallId,
                    ImportTime = DateTime.Now,
                    MjsonData = jsonContent,
                    Priority = ProcessPriority.中,
                    Status = ProcessStatus.待加工
                };
            }
            catch
            {
                return null;
            }
        }

        // ==================== 导出 CSV ====================
        private static async Task ExportCsvAsync(string filePath, List<WallListItem> data)
        {
            await using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync("房屋编号,楼层,墙体ID,导入时间,加工优先级,加工状态");

            foreach (var item in data)
            {
                await writer.WriteLineAsync(
                    $"\"{item.HouseNumber}\",{item.Floor},\"{item.WallId}\"," +
                    $"\"{item.ImportTime:yyyy-MM-dd HH:mm:ss}\",{item.Priority},{item.Status}");
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
                Priority = x.Priority.ToString(),
                Status = x.Status.ToString()
            }).ToList();

            var json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
        }

        // ==================== Mock 数据 ====================
        private void LoadMockData()
        {
            var random = new Random(42);
            var houseNumbers = new[] { "A栋-01", "A栋-02", "B栋-01", "B栋-02", "C栋-01" };
            var statuses = Enum.GetValues<ProcessStatus>();
            var priorities = Enum.GetValues<ProcessPriority>();

            var mockItems = new List<WallListItem>();

            foreach (var house in houseNumbers)
            {
                for (int floor = 1; floor <= 5; floor++)
                {
                    int wallCount = random.Next(3, 8);
                    for (int w = 0; w < wallCount; w++)
                    {
                        var wallId = $"{house}-F{floor:D2}-W{w + 1:D3}";
                        var importTime = DateTime.Now
                            .AddDays(-random.Next(0, 60))
                            .AddHours(-random.Next(0, 24))
                            .AddMinutes(-random.Next(0, 60));

                        // 生成一个简单的 mjson 数据
                        var mjsonObj = new
                        {
                            id = wallId,
                            houseNumber = house,
                            floor,
                            thickness = random.Next(150, 300),
                            length = Math.Round(random.NextDouble() * 5000 + 1000, 0),
                            height = Math.Round(random.NextDouble() * 3000 + 2000, 0),
                            material = "C30",
                            features = new[]
                            {
                                new { type = "hole", position = new { x = 500.0, y = 300.0 }, diameter = 100 }
                            }
                        };

                        var mjson = JsonSerializer.Serialize(mjsonObj);

                        mockItems.Add(new WallListItem
                        {
                            HouseNumber = house,
                            Floor = floor,
                            WallId = wallId,
                            ImportTime = importTime,
                            MjsonData = mjson,
                            Priority = priorities[random.Next(priorities.Length)],
                            Status = statuses[random.Next(statuses.Length)]
                        });
                    }
                }
            }

            _allItems = mockItems.OrderByDescending(x => x.ImportTime).ToList();
        }
    }
}
