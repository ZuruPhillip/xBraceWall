using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CncWallStation.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CncWallStation.ViewModels
{
    public partial class MarkExceptionViewModel : ObservableObject
    {
        private readonly ILogger<MarkExceptionViewModel> _logger;
        private readonly IMachiningAppService _machiningAppService;
        private readonly IExceptionReportAppService _exceptionReportService;

        // ═══════════════ 墙体搜索 ═══════════════
        [ObservableProperty] private string _searchKeyword = string.Empty;
        [ObservableProperty] private bool _isSearching;
        [ObservableProperty] private bool _hasSearchResults;

        public ObservableCollection<WallQueueItemDto> SearchResults { get; } = new();

        [ObservableProperty] private WallQueueItemDto? _selectedWall;

        // ═══════════════ 异常类型 ═══════════════
        [ObservableProperty] private int _selectedExceptionType;
        [ObservableProperty] private string _customExceptionType = string.Empty;
        [ObservableProperty] private bool _isCustomTypeVisible;

        public ObservableCollection<ExceptionTypeItem> ExceptionTypes { get; } = new()
        {
            new() { Name = "主轴异常", Value = 0 },
            new() { Name = "PLC通讯异常", Value = 1 },
            new() { Name = "刀具断裂", Value = 2 },
            new() { Name = "材料缺陷", Value = 3 },
            new() { Name = "安全门异常", Value = 4 },
            new() { Name = "进给异常", Value = 5 },
            new() { Name = "其他", Value = 6 }
        };

        // ═══════════════ 描述 ═══════════════
        [ObservableProperty] private string _description = string.Empty;

        // ═══════════════ 操作人 ═══════════════
        [ObservableProperty] private string _operatorName = "操作员";

        // ═══════════════ 照片 ═══════════════
        public ObservableCollection<BitmapImage> PhotoPreviews { get; } = new();
        private readonly List<string> _photoPaths = new();
        [ObservableProperty] private bool _hasPhotos;

        // ═══════════════ 标题 ═══════════════
        [ObservableProperty] private string _title = "标记异常";

        partial void OnSelectedExceptionTypeChanged(int value)
        {
            IsCustomTypeVisible = value == 6; // 其他
        }

        public MarkExceptionViewModel(
            ILogger<MarkExceptionViewModel> logger,
            IMachiningAppService machiningAppService,
            IExceptionReportAppService exceptionReportService)
        {
            _logger = logger;
            _machiningAppService = machiningAppService;
            _exceptionReportService = exceptionReportService;
        }

        /// <summary>初始化参数（由调用方设置）</summary>
        public void Initialize(string operatorName, string? defaultWallId, string? windowTitle = null)
        {
            OperatorName = string.IsNullOrWhiteSpace(operatorName) ? "操作员" : operatorName;

            var titlePrefix = string.IsNullOrWhiteSpace(windowTitle) ? "标记异常" : windowTitle;

            if (!string.IsNullOrWhiteSpace(defaultWallId))
            {
                SearchKeyword = defaultWallId;
                Title = $"{titlePrefix} - {defaultWallId}";
                _ = SearchWallsAsync();
            }
            else
            {
                Title = titlePrefix;
            }
        }

        // ═══════════════ 搜索命令 ═══════════════
        [RelayCommand]
        private async Task SearchWallsAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchKeyword))
            {
                SearchResults.Clear();
                return;
            }

            IsSearching = true;
            try
            {
                var results = await _machiningAppService.SearchWallsByWallIdAsync(SearchKeyword.Trim());
                SearchResults.Clear();
                foreach (var r in results)
                    SearchResults.Add(r);
                HasSearchResults = SearchResults.Count > 0;

                _logger.LogInformation("墙体搜索完成: keyword={Keyword}, count={Count}",
                    SearchKeyword, results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "墙体搜索失败");
                MessageBox.Show($"搜索失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSearching = false;
            }
        }

        // ═══════════════ 照片命令 ═══════════════
        [RelayCommand]
        private void UploadPhoto()
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择异常现场照片",
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    try
                    {
                        var dir = Path.Combine(AppContext.BaseDirectory, "output", "exceptions");
                        Directory.CreateDirectory(dir);
                        var destName = $"{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(file)}";
                        var destPath = Path.Combine(dir, destName);
                        File.Copy(file, destPath, overwrite: true);

                        _photoPaths.Add(destPath);

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(file);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        PhotoPreviews.Add(bmp);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "照片上传失败: {File}", file);
                    }
                }
                HasPhotos = PhotoPreviews.Count > 0;
            }
        }

        [RelayCommand]
        private void ClearPhotos()
        {
            PhotoPreviews.Clear();
            _photoPaths.Clear();
            HasPhotos = false;
        }

        // ═══════════════ 提交命令 ═══════════════
        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (SelectedWall == null)
            {
                MessageBox.Show("请先搜索并选择墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                MessageBox.Show("请填写异常原因描述", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var photoPathsJson = _photoPaths.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(_photoPaths)
                    : null;

                var entity = new MachiningExceptionEntity(
                    SelectedWall.Id,
                    SelectedExceptionType,
                    Description,
                    OperatorName,
                    SelectedExceptionType == 6 ? CustomExceptionType : null,
                    photoPathsJson);

                await _exceptionReportService.SaveReportAsync(entity);
                _logger.LogInformation("异常报告已保存: WallId={WallId}", SelectedWall.WallId);

                MessageBox.Show("异常报告已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提交异常报告失败");
                MessageBox.Show($"提交失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════ 取消命令 ═══════════════
        [RelayCommand]
        private void Cancel()
        {
            CloseWindow(false);
        }

        private void CloseWindow(bool result)
        {
            var window = Application.Current.Windows
                .OfType<MarkExceptionWindow>()
                .FirstOrDefault();
            if (window != null)
            {
                window.DialogResult = result;
                window.Close();
            }
        }
    }
}
