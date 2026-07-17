using BimWallData;
using BimWallData.V000;
using CncWallStation.Commands;
using CncWallStation.Consts;
using CncWallStation.Localization;
using CncWallStation.Models.Dtos;
using CncWallStation.Services.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace CncWallStation.ViewModels
{
    /// <summary>
    /// BimDataRenderPage 的 ViewModel
    /// 负责墙体搜索（按WallId+最新版本）、数据加载管道、图层状态、悬停数据
    /// </summary>
    public partial class BimDataRenderViewModel : ObservableObject
    {
        private readonly IWallAppService _wallAppService;
        private readonly BimJsonDeserializer _deserializer;
        private readonly ILogger<BimDataRenderViewModel> _logger;

        // ── 构造函数（DI 注入） ──

        public BimDataRenderViewModel(
            IWallAppService wallAppService,
            BimJsonDeserializer deserializer,
            ILogger<BimDataRenderViewModel> logger)
        {
            _wallAppService = wallAppService;
            _deserializer = deserializer;
            _logger = logger;

            HtmlFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                "Htmls",
                "BimWall3D.html");

            InitializeCommands();
        }

        // ──────────────────────────────────────────
        //  状态属性
        // ──────────────────────────────────────────

        private string _statusMessage = Localization.LocalizationService.Instance["Status_BimReady"];
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _htmlFilePath = string.Empty;
        public string HtmlFilePath
        {
            get => _htmlFilePath;
            set => SetProperty(ref _htmlFilePath, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                SetProperty(ref _isLoading, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isRendering;
        public bool IsRendering
        {
            get => _isRendering;
            set => SetProperty(ref _isRendering, value);
        }

        private bool _isPageLoaded;
        public bool IsPageLoaded
        {
            get => _isPageLoaded;
            set => SetProperty(ref _isPageLoaded, value);
        }

        // ──────────────────────────────────────────
        //  墙体搜索与选取
        // ──────────────────────────────────────────

        private string _searchWallId = string.Empty;
        public string SearchWallId
        {
            get => _searchWallId;
            set => SetProperty(ref _searchWallId, value);
        }

        private WallDto? _foundWall;
        public WallDto? FoundWall
        {
            get => _foundWall;
            set
            {
                if (SetProperty(ref _foundWall, value))
                {
                    IsWallFound = value != null;
                    UpdateDetailInfo();
                    OnPropertyChanged(nameof(CanLoadRender));
                }
            }
        }

        private bool _isWallFound;
        public bool IsWallFound
        {
            get => _isWallFound;
            set => SetProperty(ref _isWallFound, value);
        }

        /// <summary>是否可加载渲染（有墙体且非加载中）</summary>
        public bool CanLoadRender => !IsLoading && FoundWall != null;

        // ──────────────────────────────────────────
        //  墙体详情信息（搜索栏下一行展示）
        // ──────────────────────────────────────────

        private string _detailWallId = "—";
        public string DetailWallId
        {
            get => _detailWallId;
            set => SetProperty(ref _detailWallId, value);
        }

        private string _detailProject = "—";
        public string DetailProject
        {
            get => _detailProject;
            set => SetProperty(ref _detailProject, value);
        }

        private string _detailFloor = "—";
        public string DetailFloor
        {
            get => _detailFloor;
            set => SetProperty(ref _detailFloor, value);
        }

        private string _detailStage = "—";
        public string DetailStage
        {
            get => _detailStage;
            set => SetProperty(ref _detailStage, value);
        }

        private string _detailVersion = "—";
        public string DetailVersion
        {
            get => _detailVersion;
            set => SetProperty(ref _detailVersion, value);
        }

        private string _detailImportTime = "—";
        public string DetailImportTime
        {
            get => _detailImportTime;
            set => SetProperty(ref _detailImportTime, value);
        }

        // ──────────────────────────────────────────
        //  图层可见性状态
        // ──────────────────────────────────────────

        private bool _isWallVisible = true;
        public bool IsWallVisible
        {
            get => _isWallVisible;
            set
            {
                if (SetProperty(ref _isWallVisible, value))
                    ExecuteLayerToggle("wall", value);
            }
        }

        private bool _isBrickVisible = true;
        public bool IsBrickVisible
        {
            get => _isBrickVisible;
            set
            {
                if (SetProperty(ref _isBrickVisible, value))
                    ExecuteLayerToggle("slices", value);
            }
        }

        private bool _isJointVisible = true;
        public bool IsJointVisible
        {
            get => _isJointVisible;
            set
            {
                if (SetProperty(ref _isJointVisible, value))
                    ExecuteLayerToggle("joints", value);
            }
        }

        private bool _isDimVisible = true;
        public bool IsDimVisible
        {
            get => _isDimVisible;
            set
            {
                if (SetProperty(ref _isDimVisible, value))
                    ExecuteLayerToggle("dim", value);
            }
        }

        private bool _isRebarVisible = true;
        public bool IsRebarVisible
        {
            get => _isRebarVisible;
            set
            {
                if (SetProperty(ref _isRebarVisible, value))
                    ExecuteLayerToggle("rebars", value);
            }
        }

        // ──────────────────────────────────────────
        //  悬停浮窗数据（边缘测量）
        // ──────────────────────────────────────────

        private EdgeMeasurementInfo? _edgeMeasurement;
        public EdgeMeasurementInfo? EdgeMeasurement
        {
            get => _edgeMeasurement;
            set => SetProperty(ref _edgeMeasurement, value);
        }

        private bool _isEdgeTipVisible;
        public bool IsEdgeTipVisible
        {
            get => _isEdgeTipVisible;
            set => SetProperty(ref _isEdgeTipVisible, value);
        }

        // ──────────────────────────────────────────
        //  砖块悬停浮窗数据
        // ──────────────────────────────────────────

        private BrickHoverInfo? _brickHover;
        public BrickHoverInfo? BrickHover
        {
            get => _brickHover;
            set => SetProperty(ref _brickHover, value);
        }

        private bool _isBrickTipVisible;
        public bool IsBrickTipVisible
        {
            get => _isBrickTipVisible;
            set => SetProperty(ref _isBrickTipVisible, value);
        }

        // ──────────────────────────────────────────
        //  命令
        // ──────────────────────────────────────────

        public RelayCommand SearchWallCommand { get; private set; } = null!;
        public RelayCommand LoadRenderCommand { get; private set; } = null!;
        public RelayCommand ResetViewCommand { get; private set; } = null!;
        public RelayCommand ExportCommand { get; private set; } = null!;

        // ──────────────────────────────────────────
        //  供 View 注入的委托
        // ──────────────────────────────────────────

        public Func<string, Task>? ExecuteScriptAsync { get; set; }
        public Func<Task>? NavigateToHtml { get; set; }

        // ══════════════════════════════════════════
        //  初始化命令
        // ══════════════════════════════════════════

        private void InitializeCommands()
        {
            SearchWallCommand = new RelayCommand(
                execute: _ => _ = SearchWallAsync(),
                canExecute: _ => !IsLoading);

            LoadRenderCommand = new RelayCommand(
                execute: _ => _ = LoadRenderAsync(),
                canExecute: _ => !IsLoading && FoundWall != null);

            ResetViewCommand = new RelayCommand(
                execute: _ => _ = ExecuteResetViewAsync(),
                canExecute: _ => !IsLoading && IsPageLoaded);

            ExportCommand = new RelayCommand(
                execute: _ => ExecuteExport(),
                canExecute: _ => !IsLoading);
        }

        // ══════════════════════════════════════════
        //  墙体搜索（按 WallId + 最新版本）
        // ══════════════════════════════════════════

        private async Task SearchWallAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchWallId))
            {
                StatusMessage = LocalizationService.Instance["Status_EnterWallId"];
                return;
            }

            IsLoading = true;
            FoundWall = null;
            StatusMessage = LocalizationService.Instance["Status_SearchingWall"];

            try
            {
                var input = new WallQueryInput
                {
                    WallId = SearchWallId.Trim(),
                    Page = 1,
                    PageSize = 1,
                    LatestOnly = true
                };

                var result = await _wallAppService.QueryWallsAsync(input);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (result.TotalCount > 0 && result.Items.Count > 0)
                    {
                        FoundWall = result.Items[0];
                        StatusMessage = string.Format(LocalizationService.Instance["Status_FoundWall"], FoundWall.WallId);
                    }
                    else
                    {
                        StatusMessage = LocalizationService.Instance["Status_WallNotFound"];
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索墙体失败");
                StatusMessage = string.Format(LocalizationService.Instance["Status_SearchFailed"], ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateDetailInfo()
        {
            if (FoundWall == null)
            {
                DetailWallId = "—";
                DetailProject = "—";
                DetailFloor = "—";
                DetailStage = "—";
                DetailVersion = "—";
                DetailImportTime = "—";
                return;
            }

            DetailWallId = FoundWall.WallId;
            DetailProject = FoundWall.ProjectName;
            DetailFloor = string.Format(Localization.LocalizationService.Instance["DetailFormat_Floor"], FoundWall.Floor);
            DetailStage = FoundWall.PipelineStageText;
            DetailVersion = $"v{FoundWall.SchemaVersion}";
            DetailImportTime = FoundWall.ImportTime.ToString("yyyy-MM-dd HH:mm");
        }

        // ══════════════════════════════════════════
        //  数据加载与渲染（核心管道）
        // ══════════════════════════════════════════

        private async Task LoadRenderAsync()
        {
            if (FoundWall == null)
            {
                StatusMessage = LocalizationService.Instance["Status_NoWallToLoad"];
                return;
            }

            if (!File.Exists(HtmlFilePath))
            {
                StatusMessage = $"❌ 找不到渲染文件: {HtmlFilePath}";
                return;
            }

            IsLoading = true;
            IsRendering = false;
            StatusMessage = "🔄 正在加载墙体数据...";

            try
            {
                _logger.LogInformation("加载墙体详情: WallId={WallId}", FoundWall.WallId);
                var detail = await _wallAppService.GetDetailAsync(FoundWall.Id);

                if (detail == null || string.IsNullOrWhiteSpace(detail.BimJsonData))
                {
                    StatusMessage = "❌ 该墙体没有 BimJSON 数据";
                    IsLoading = false;
                    return;
                }

                StatusMessage = "🔄 正在解析 BimJSON 数据...";

                var version = BimJsonDeserializer.ExtractVersion(detail.BimJsonData);
                _logger.LogInformation("BimJSON 版本: v{Version}", version);

                var dto = _deserializer.DeserializeOrThrow(detail.BimJsonData);
                dto.Validate();

                StatusMessage = $"🔄 BimJSON 解析成功 (v{version})，正在映射渲染数据...";

                MapToDObject(dto);

                IsRendering = true;
                if (NavigateToHtml != null)
                    await NavigateToHtml();

                StatusMessage = $"🔄 正在注入渲染数据 (v{version})...";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载渲染数据失败");
                StatusMessage = $"❌ 加载失败: {ex.Message}";
                IsLoading = false;
                IsRendering = false;
            }
        }

        /// <summary>
        /// 供外部调用：通过 wallId 搜索墙体并自动加载渲染（无需手动点"加载渲染"按钮）
        /// </summary>
        public async Task SearchAndLoadAsync(string wallId)
        {
            if (string.IsNullOrWhiteSpace(wallId)) return;

            SearchWallId = wallId;
            IsLoading = true;
            FoundWall = null;
            StatusMessage = LocalizationService.Instance["Status_SearchingWall"];

            try
            {
                var input = new WallQueryInput
                {
                    WallId = wallId.Trim(),
                    Page = 1,
                    PageSize = 1,
                    LatestOnly = true
                };
                var result = await _wallAppService.QueryWallsAsync(input);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (result.TotalCount > 0 && result.Items.Count > 0)
                    {
                        FoundWall = result.Items[0];
                        StatusMessage = string.Format(LocalizationService.Instance["Status_FoundWall"], FoundWall.WallId);
                    }
                    else
                    {
                        StatusMessage = LocalizationService.Instance["Status_WallNotFound"];
                        IsLoading = false;
                    }
                });

                if (FoundWall != null)
                {
                    LoadRenderCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索并加载墙体失败");
                StatusMessage = string.Format(LocalizationService.Instance["Status_SearchFailed"], ex.Message);
                IsLoading = false;
            }
        }

        public async void OnPageLoaded()
        {
            IsPageLoaded = true;

            if (!IsRendering)
            {
                IsLoading = false;
                StatusMessage = LocalizationService.Instance["Status_PageReady"];
                return;
            }

            if (ExecuteScriptAsync != null && !string.IsNullOrEmpty(_cachedDObjectJson))
            {
                try
                {
                    var escaped = _cachedDObjectJson.Replace("\\", "\\\\").Replace("'", "\\'");
                    await ExecuteScriptAsync($"loadWallData('{escaped}')");
                    StatusMessage = LocalizationService.Instance["Status_BimModelLoaded"];
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "注入渲染数据失败");
                    StatusMessage = $"❌ 渲染数据注入失败: {ex.Message}";
                }
            }

            IsLoading = false;
            IsRendering = false;
            _cachedDObjectJson = string.Empty;
        }

        private string _cachedDObjectJson = string.Empty;

        public void OnPageFailed(string error)
        {
            IsLoading = false;
            IsRendering = false;
            IsPageLoaded = false;
            _cachedDObjectJson = string.Empty;
            StatusMessage = $"❌ 页面加载失败: {error}";
        }

        // ══════════════════════════════════════════
        //  BimWallDtoBase → Three.js D 对象映射
        // ══════════════════════════════════════════

        private void MapToDObject(BimWallDtoBase dto)
        {
            var contour = dto.AacWallElevation!.Contour
                .Select(p => new { x = p.X, y = p.Y })
                .ToList();

            var slices = new List<object>();
            if (dto is BimWallDtoV000 v000 && v000.AacSlices != null)
            {
                foreach (var sl in v000.AacSlices)
                {
                    var slContour = sl.Contour
                        .Select(p => new { x = p.X, y = p.Y })
                        .ToList();

                    var glueSegs = sl.GlueSegments?
                        .Select(g => new
                        {
                            s = new { x = g.StartPoint.X, y = g.StartPoint.Y },
                            e = new { x = g.EndPoint.X, y = g.EndPoint.Y }
                        })
                        .ToList();

                    slices.Add(new
                    {
                        contour = slContour,
                        col = sl.SliceColumn,
                        gluePn = v000.Pn,
                        glueSegs = glueSegs,
                        pn = sl.Id
                    });
                }
            }

            var rebarSlots = new List<object>();
            if (dto is BimWallDtoV000 vRebar && vRebar.Rebars?.Rods != null)
            {
                var rebar = vRebar.Rebars;
                foreach (var rod in rebar.Rods)
                {
                    if (rod.StartPoint == null || rod.EndPoint == null) continue;
                    rebarSlots.Add(new
                    {
                        startX = rod.StartPoint.X,
                        startY = rod.StartPoint.Y,
                        startZ = rod.StartPoint.Z,
                        endX = rod.EndPoint.X,
                        endY = rod.EndPoint.Y,
                        endZ = rod.EndPoint.Z,
                        diameter = WallConstants.RebarSlotWidth,
                        horizontalDepth = rebar.HorizontalDepth,
                        verticalDepth = rebar.VerticalDepth,
                        pn = rebar.Pn
                    });
                }
            }

            var dObject = new
            {
                wallContour = contour,
                thickness = dto.CoreThickness,
                slices = slices,
                rebarSlots = rebarSlots
            };

            _cachedDObjectJson = JsonConvert.SerializeObject(dObject, Formatting.None);
        }

        // ══════════════════════════════════════════
        //  图层切换
        // ══════════════════════════════════════════

        private async void ExecuteLayerToggle(string layerName, bool visible)
        {
            if (ExecuteScriptAsync != null && IsPageLoaded)
            {
                try
                {
                    var vis = visible ? "true" : "false";
                    await ExecuteScriptAsync($"toggleLayer('{layerName}', {vis})");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "图层切换失败: {Layer}", layerName);
                }
            }
        }

        // ══════════════════════════════════════════
        //  视角重置
        // ══════════════════════════════════════════

        private async Task ExecuteResetViewAsync()
        {
            if (ExecuteScriptAsync != null && IsPageLoaded)
            {
                await ExecuteScriptAsync("resetCamera()");
                StatusMessage = "✅ 视角已重置";
            }
            else
            {
                StatusMessage = "⚠️ 页面尚未加载";
            }
        }

        // ══════════════════════════════════════════
        //  导出
        // ══════════════════════════════════════════

        private void ExecuteExport()
        {
            MessageBox.Show(
                "可在墙体3D模型中使用 renderer.domElement.toDataURL() 导出 PNG 截图。",
                "导出提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            StatusMessage = "ℹ️ 导出功能已触发";
        }

        // ══════════════════════════════════════════
        //  HTML postMessage 处理
        // ══════════════════════════════════════════

        public void HandleWebMessage(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message)) return;

                if (message.StartsWith("edgeMeasure:"))
                {
                    // "edgeMeasure:" = 12 字符
                    HandleEdgeMeasure(message.Length > 12 ? message[12..] : "");
                }
                else if (message.StartsWith("status:"))
                {
                    // "status:" = 7 字符
                    var status = message.Length > 7 ? message[7..] : "";
                    if (!string.IsNullOrEmpty(status))
                        StatusMessage = status;
                }
                // 砖块悬浮由 HTML 自管，不再通过 postMessage 回传
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "处理 WebMessage 异常");
            }
        }

        private void HandleEdgeMeasure(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                EdgeMeasurement = null;
                IsEdgeTipVisible = false;
                return;
            }
            try
            {
                var data = JsonConvert.DeserializeAnonymousType(json, new
                {
                    type = "",
                    length = 0.0,
                    start = new { x = 0.0, y = 0.0, z = 0.0 },
                    end = new { x = 0.0, y = 0.0, z = 0.0 }
                });
                if (data != null)
                {
                    EdgeMeasurement = new EdgeMeasurementInfo
                    {
                        EdgeType = data.type,
                        Length = $"{data.length:F1} mm",
                        StartPoint = $"({data.start.x:F1}, {data.start.y:F1}, {data.start.z:F1})",
                        EndPoint = $"({data.end.x:F1}, {data.end.y:F1}, {data.end.z:F1})"
                    };
                    IsEdgeTipVisible = true;
                }
            }
            catch { }
        }

    }


    // ══════════════════════════════════════════════
    //  辅助数据模型
    // ══════════════════════════════════════════════

    public class EdgeMeasurementInfo
    {
        public string EdgeType { get; set; } = string.Empty;
        public string Length { get; set; } = string.Empty;
        public string StartPoint { get; set; } = string.Empty;
        public string EndPoint { get; set; } = string.Empty;
    }

    public class BrickHoverInfo
    {
        public string Column { get; set; } = string.Empty;
        public string Width { get; set; } = string.Empty;
        public string Height { get; set; } = string.Empty;
        public string Thickness { get; set; } = string.Empty;
        public string GlueInfo { get; set; } = string.Empty;
        public string Pn { get; set; } = string.Empty;
    }
}
