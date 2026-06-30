using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CncWallStation.ViewModels
{
    public partial class ExceptionDetailViewModel : ObservableObject
    {
        private readonly ILogger<ExceptionDetailViewModel> _logger;
        private readonly IExceptionReportAppService _exceptionReportService;

        // ═══════════════ 模式 ═══════════════
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private string _title = "异常详情";

        // ═══════════════ 基本信息（只读） ═══════════════
        [ObservableProperty] private long _reportId;
        [ObservableProperty] private string _wallIdStr = string.Empty;
        [ObservableProperty] private string _operatorName = string.Empty;
        [ObservableProperty] private string _createdAt = string.Empty;
        [ObservableProperty] private bool _isResolved;
        [ObservableProperty] private string _statusText = string.Empty;

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

        // ═══════════════ 照片 ═══════════════
        public ObservableCollection<BitmapImage> PhotoPreviews { get; } = new();
        private readonly List<string> _photoPaths = new();
        [ObservableProperty] private bool _hasPhotos;

        partial void OnSelectedExceptionTypeChanged(int value)
        {
            IsCustomTypeVisible = value == 6; // 其他
        }

        public ExceptionDetailViewModel(
            ILogger<ExceptionDetailViewModel> logger,
            IExceptionReportAppService exceptionReportService)
        {
            _logger = logger;
            _exceptionReportService = exceptionReportService;
        }

        /// <summary>初始化查看模式</summary>
        public void InitializeForView(ExceptionItemViewModel item)
        {
            IsEditMode = false;
            Title = "异常详情 - 查看";
            PopulateFromItem(item);
        }

        /// <summary>初始化编辑模式</summary>
        public void InitializeForEdit(ExceptionItemViewModel item)
        {
            IsEditMode = true;
            Title = "异常详情 - 编辑";
            PopulateFromItem(item);
        }

        private void PopulateFromItem(ExceptionItemViewModel item)
        {
            ReportId = item.Id;
            WallIdStr = item.WallIdStr;
            OperatorName = item.Operator;
            CreatedAt = item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            IsResolved = item.IsResolved;
            StatusText = item.IsResolved ? "已解决" : "未解决";
            Description = item.Description;

            // 异常类型
            if (!string.IsNullOrWhiteSpace(item.CustomType))
            {
                SelectedExceptionType = 6; // 其他
                CustomExceptionType = item.CustomType;
            }
            else
            {
                SelectedExceptionType = item.ExceptionType;
            }

            // 加载已有照片
            if (!string.IsNullOrWhiteSpace(item.PhotoPaths))
            {
                try
                {
                    var paths = JsonSerializer.Deserialize<List<string>>(item.PhotoPaths);
                    if (paths != null)
                    {
                        foreach (var path in paths)
                        {
                            if (File.Exists(path))
                            {
                                _photoPaths.Add(path);
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = new Uri(path);
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                bmp.Freeze();
                                PhotoPreviews.Add(bmp);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "加载异常报告照片失败: Id={Id}", ReportId);
                }
            }
            HasPhotos = PhotoPreviews.Count > 0;
        }

        // ═══════════════ 照片上传 ═══════════════
        [RelayCommand]
        private void UploadPhoto()
        {
            if (!IsEditMode) return;

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
            if (!IsEditMode) return;

            PhotoPreviews.Clear();
            _photoPaths.Clear();
            HasPhotos = false;
        }

        // ═══════════════ 保存编辑 ═══════════════
        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                MessageBox.Show("请填写异常原因描述", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var photoPathsJson = _photoPaths.Count > 0
                    ? JsonSerializer.Serialize(_photoPaths)
                    : null;

                string? customType = SelectedExceptionType == 6 ? CustomExceptionType : null;

                await _exceptionReportService.UpdateReportAsync(
                    ReportId,
                    SelectedExceptionType,
                    customType,
                    Description,
                    photoPathsJson);

                _logger.LogInformation("异常报告已更新: Id={Id}", ReportId);

                MessageBox.Show("异常报告已更新", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新异常报告失败");
                MessageBox.Show($"更新失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════ 关闭 ═══════════════
        [RelayCommand]
        private void Close()
        {
            CloseWindow(false);
        }

        private void CloseWindow(bool result)
        {
            var window = Application.Current.Windows
                .OfType<Views.ExceptionDetailWindow>()
                .FirstOrDefault();
            if (window != null)
            {
                window.DialogResult = result;
                window.Close();
            }
        }
    }
}
