using CncWallStation.Commands;
using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.Features.MepSlots;
using CncWallStation.Localization;
using CncWallStation.Models.Dtos;
using CncWallStation.MomWallData;
using CncWallStation.Services.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace CncWallStation.ViewModels
{
    /// <summary>
    /// MomDataRenderPage 的 ViewModel
    /// 负责墙体搜索（按WallId+最新版本）、MomJsonData 数据加载管道、
    /// 图层状态（墙体/腔体/MEP槽/标注）、CSG 渲染引擎控制
    /// </summary>
    public partial class MomDataRenderViewModel : ObservableObject
    {
        private readonly IWallAppService _wallAppService;
        private readonly ILogger<MomDataRenderViewModel> _logger;

        // ── 构造函数（DI 注入） ──

        public MomDataRenderViewModel(
            IWallAppService wallAppService,
            ILogger<MomDataRenderViewModel> logger)
        {
            _wallAppService = wallAppService;
            _logger = logger;

            HtmlFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                "Htmls",
                "MomWall3D.html");

            InitializeCommands();
        }

        // ──────────────────────────────────────────
        //  状态属性
        // ──────────────────────────────────────────

        private string _statusMessage = Localization.LocalizationService.Instance["Status_MomReady"];
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

        // ── 翻面 / 原点变换状态 ──

        private string _originalMomJsonData = string.Empty;
        private string _originalDObjectJson = string.Empty;

        /// <summary>重建渲染并发锁，确保翻面/原点变换串行执行</summary>
        private readonly SemaphoreSlim _rebuildLock = new SemaphoreSlim(1, 1);

        private bool _isFlipped;
        public bool IsFlipped
        {
            get => _isFlipped;
            set
            {
                if (SetProperty(ref _isFlipped, value))
                    _ = RebuildRenderAsync();
            }
        }

        private bool _isOriginTransformed;
        public bool IsOriginTransformed
        {
            get => _isOriginTransformed;
            set
            {
                if (SetProperty(ref _isOriginTransformed, value))
                    _ = RebuildRenderAsync();
            }
        }

        private bool _isWallDataLoaded;
        public bool IsWallDataLoaded
        {
            get => _isWallDataLoaded;
            set => SetProperty(ref _isWallDataLoaded, value);
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
        //  墙体详情信息
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

        private bool _isGrooveVisible = true;
        public bool IsGrooveVisible
        {
            get => _isGrooveVisible;
            set
            {
                if (SetProperty(ref _isGrooveVisible, value))
                    ExecuteLayerToggle("groove", value);
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

                if (detail == null || string.IsNullOrWhiteSpace(detail.MomJsonData))
                {
                    StatusMessage = "❌ 该墙体没有 MomJSON 数据";
                    IsLoading = false;
                    return;
                }

                // ★ 保存原始 MomJSON 数据，供翻面/原点变换切换使用
                _originalMomJsonData = detail.MomJsonData;
                // 重置翻面和原点变换状态（通过 backing field 避免触发 RebuildRenderAsync）
                _isFlipped = false;
                _isOriginTransformed = false;
                OnPropertyChanged(nameof(IsFlipped));
                OnPropertyChanged(nameof(IsOriginTransformed));

                StatusMessage = "🔄 正在解析 MomJSON 数据...";

                // 反序列化 MomWall（需与序列化侧保持一致，枚举字段用字符串形式）
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                var momWall = System.Text.Json.JsonSerializer.Deserialize<MomWall>(detail.MomJsonData, options);

                if (momWall == null || momWall.Outline == null || momWall.Outline.Count == 0)
                {
                    StatusMessage = "❌ MomJSON 解析失败或轮廓数据为空";
                    IsLoading = false;
                    return;
                }

                // ★ 反序列化后恢复 Face：Face 标记为 [JsonIgnore]，需从 InitialSide 重建
                foreach (var f in momWall.Features)
                    f.RestoreFaceFromInitialSide();

                StatusMessage = "🔄 MomJSON 解析成功，正在映射渲染数据...";

                MapToDObject(momWall);

                // ★ 保存原始渲染数据 JSON，供取消原点变换时恢复
                _originalDObjectJson = _cachedDObjectJson;
                IsWallDataLoaded = true;

                IsRendering = true;
                if (NavigateToHtml != null)
                    await NavigateToHtml();

                StatusMessage = "🔄 正在注入渲染数据...";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载渲染数据失败");
                StatusMessage = $"❌ 加载失败: {ex.Message}";
                IsLoading = false;
                IsRendering = false;
                IsWallDataLoaded = false;
                _originalMomJsonData = string.Empty;
                _originalDObjectJson = string.Empty;
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
                    StatusMessage = LocalizationService.Instance["Status_MomModelLoaded"];
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
            IsWallDataLoaded = false;
            _cachedDObjectJson = string.Empty;
            _originalMomJsonData = string.Empty;
            _originalDObjectJson = string.Empty;
            // 重置 toggle 状态（通过 backing field 避免触发 RebuildRenderAsync）
            _isFlipped = false;
            _isOriginTransformed = false;
            OnPropertyChanged(nameof(IsFlipped));
            OnPropertyChanged(nameof(IsOriginTransformed));
            StatusMessage = $"❌ 页面加载失败: {error}";
        }

        // ══════════════════════════════════════════
        //  MomWall → Three.js D 对象映射
        // ══════════════════════════════════════════

        private void MapToDObject(MomWall momWall)
        {
            // 提取轮廓顶点
            var outline = momWall.Outline
                .Select(p => new { x = p.X, y = p.Y })
                .ToList();

            // 计算实际尺寸
            var (minX, minY, maxX, maxY) = momWall.GetOutlineBounds();
            float actualLength = momWall.ActualLength;
            float actualWidth = momWall.ActualWidth;
            float actualThickness = momWall.ActualThickness;

            // 提取特征: Groove、MepSlot、Pocket、Hole
            var features = new List<object>();

            foreach (var feature in momWall.Features)
            {
                if (feature is Groove groove)
                {
                    features.Add(SerializeGroove(groove));
                }
                else if (feature is MepSlot mepSlot)
                {
                    features.Add(SerializeMepSlot(mepSlot));
                }
                else if (feature is Pocket pocket)
                {
                    features.Add(SerializePocket(pocket));
                }
                else if (feature is Hole hole)
                {
                    features.Add(SerializeHole(hole));
                }
                else if (feature is RebarSlot rebarSlot)
                {
                    features.Add(SerializeRebarSlot(rebarSlot));
                }
            }

            var material = momWall.Material ?? "AAC";

            var dObject = new
            {
                actualLength,
                actualWidth,
                actualThickness,
                material,
                thickness = actualThickness,
                length = actualLength,
                obbLength = momWall.ObbLength,
                obbWidth = momWall.ObbWidth,
                features
            };

            _cachedDObjectJson = JsonConvert.SerializeObject(dObject, Formatting.None);
        }

        /// <summary>将 GrooveType 枚举转为 HTML 引擎识别的 key（首字母小写驼峰）</summary>
        private static string MapGrooveTypeKey(GrooveType grooveType)
        {
            return grooveType switch
            {
                GrooveType.SteelColumn => "steelColumn",
                GrooveType.TopBracket => "topBracket",
                GrooveType.BaseBracket => "baseBracket",
                GrooveType.TopPlate => "topPlate",
                GrooveType.GlueSeal => "glueSeal",
                GrooveType.XBraceSteel => "xBraceSteel",
                GrooveType.Custom => "default",
                _ => "default"
            };
        }

        private static object SerializeGroove(Groove groove)
        {
            // 构建轮廓点集（仿 HTML 中 features.groove 的 expected 结构）
            // 方便 HTML 中使用 Points 渲染凹槽形状
            var outlinePoints = new List<object>();
            var (p0, p1, p2, p3) = groove.GetCorners();
            outlinePoints.Add(new { x = p0.X, y = p0.Y });
            outlinePoints.Add(new { x = p1.X, y = p1.Y });
            outlinePoints.Add(new { x = p2.X, y = p2.Y });
            outlinePoints.Add(new { x = p3.X, y = p3.Y });

            var normal = groove.CurrentNormal;

            return new
            {
                id = groove.Id,
                featureType = "Groove",
                grooveType = MapGrooveTypeKey(groove.GrooveType),
                startPt = new { x = groove.StartPt.X, y = groove.StartPt.Y },
                endPt = new { x = groove.EndPt.X, y = groove.EndPt.Y },
                width = groove.Width,
                depth = groove.Depth,
                length = groove.Length,
                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
                initialSide = groove.InitialSide.ToString(),
                currentSide = groove.CurrentSide.ToString(),
                outlinePoints
            };
        }

        private static object SerializeMepSlot(MepSlot mepSlot)
        {
            var normal = mepSlot.CurrentNormal;

            var segments = mepSlot.Segments.Select(seg =>
            {
                if (seg is LineSegment line)
                {
                    return (object)new
                    {
                        type = "Line",                // HTML 用 seg.type 判断段类型
                        startPoint = new { x = line.StartPoint.X, y = line.StartPoint.Y },
                        endPoint = new { x = line.EndPoint.X, y = line.EndPoint.Y },
                        depth = line.Depth,
                        length = line.Length,
                        overrideWidth = line.OverrideWidth
                    };
                }
                else if (seg is ArcSegment arc)
                {
                    return (object)new
                    {
                        type = "Arc",                 // HTML 用 seg.type 判断段类型
                        center = new { x = arc.Center.X, y = arc.Center.Y },
                        radius = arc.Radius,
                        StartAngleDeg = arc.StartAngleDeg,
                        EndAngleDeg = arc.EndAngleDeg,
                        isClockwise = arc.IsClockwise,
                        depth = arc.Depth,
                        length = arc.Length,
                        overrideWidth = arc.OverrideWidth
                    };
                }
                return null;
            }).Where(s => s != null).ToList();

            return new
            {
                id = mepSlot.Id,
                featureType = "MepSlot",
                width = mepSlot.Width,
                depth = mepSlot.MinDepth, // 最小深度作为默认值
                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
                initialSide = mepSlot.InitialSide.ToString(),
                currentSide = mepSlot.CurrentSide.ToString(),
                totalLength = mepSlot.TotalLength,
                segmentCount = mepSlot.SegmentCount,
                isUniformDepth = mepSlot.IsUniformDepth,
                pathStart = mepSlot.PathStart != null
                    ? new { x = mepSlot.PathStart.Value.X, y = mepSlot.PathStart.Value.Y }
                    : null,
                pathEnd = mepSlot.PathEnd != null
                    ? new { x = mepSlot.PathEnd.Value.X, y = mepSlot.PathEnd.Value.Y }
                    : null,
                segments
            };
        }

        /// <summary>
        /// 将 Hole 特征序列化为 HTML 渲染引擎可识别的格式。
        /// HTML 端通过 featureType==='Hole' 筛选孔特征，
        /// 通过 shape==='Round'/'Slotted' 区分圆孔/腰孔。
        /// </summary>
        private static object SerializeHole(Hole hole)
        {
            var normal = hole.CurrentNormal;

            return new
            {
                id = hole.Id,
                featureType = "Hole",
                shape = hole.Shape.ToString(),           // "Round" / "Slotted"
                radius = hole.Radius,
                depth = hole.Depth,
                slotLength = hole.SlotLength,
                slotAngleDeg = hole.SlotAngleDeg,
                throughHole = hole.ThroughHole,
                localPos = new { x = hole.LocalPos.X, y = hole.LocalPos.Y },
                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
                initialSide = hole.InitialSide.ToString(),
                currentSide = hole.CurrentSide.ToString()
            };
        }

        /// <summary>
        /// 将 Pocket 特征序列化为 Groove 格式（与 HTML Groove 渲染引擎兼容）
        /// Pocket 矩形区域沿 Length 方向映射为 Groove 的起终点，Width 作为槽宽
        /// </summary>
        private static object SerializePocket(Pocket pocket)
        {
            var normal = pocket.CurrentNormal;
            float halfLen = pocket.Length / 2f;

            // Pocket 中心在 LocalPos，沿 X 展开为起终点
            var startPt = new { x = pocket.LocalPos.X - halfLen, y = pocket.LocalPos.Y };
            var endPt = new { x = pocket.LocalPos.X + halfLen, y = pocket.LocalPos.Y };

            return new
            {
                id = pocket.Id,
                featureType = "Groove",           // 复用 Groove 渲染管线
                grooveType = "pocket",            // 图例颜色 key
                startPt,
                endPt,
                width = pocket.Width,
                depth = pocket.Depth,
                length = pocket.Length,
                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
                initialSide = pocket.InitialSide.ToString(),
                currentSide = pocket.CurrentSide.ToString(),
                outlinePoints = (object?)null       // Pocket 暂不提供角点
            };
        }

        /// <summary>
        /// 将 RebarSlot 特征序列化为 HTML 渲染引擎可识别的格式。
        /// HTML 端通过 featureType==='RebarSlot' 筛选钢筋槽特征。
        /// </summary>
        private static object SerializeRebarSlot(RebarSlot rebarSlot)
        {
            var normal = rebarSlot.CurrentNormal;

            return new
            {
                id = rebarSlot.Id,
                featureType = "RebarSlot",
                localPos = new { x = rebarSlot.LocalPos.X, y = rebarSlot.LocalPos.Y },
                endPos = new { x = rebarSlot.EndPos.X, y = rebarSlot.EndPos.Y },
                diameter = rebarSlot.Diameter,
                depth = rebarSlot.Depth,
                length = rebarSlot.Length,
                direction = rebarSlot.Direction.ToString(),    // "Vertical" / "Horizontal"
                startThreading = rebarSlot.StartThreading,
                endThreading = rebarSlot.EndThreading,
                pn = rebarSlot.Pn,
                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
                initialSide = rebarSlot.InitialSide.ToString(),
                currentSide = rebarSlot.CurrentSide.ToString()
            };
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
        //  统一重建渲染（翻面 + 原点变换可组合，并发保护）
        // ══════════════════════════════════════════

        /// <summary>
        /// 统一重建渲染方法：从原始 MomJsonData 出发，按 IsFlipped → IsOriginTransformed
        /// 顺序依次应用变换，避免重复加载 wallData。
        /// 使用 SemaphoreSlim 确保快速连续点击时方法串行执行。
        /// </summary>
        private async Task RebuildRenderAsync()
        {
            if (!IsPageLoaded || ExecuteScriptAsync == null)
                return;

            await _rebuildLock.WaitAsync();
            try
            {
                // 两个开关都关闭时，恢复原始渲染数据
                if (!IsFlipped && !IsOriginTransformed)
                {
                    if (!string.IsNullOrEmpty(_originalDObjectJson))
                    {
                        var escaped = _originalDObjectJson.Replace("\\", "\\\\").Replace("'", "\\'");
                        await ExecuteScriptAsync($"loadWallData('{escaped}')");
                        StatusMessage = "✅ 已恢复原始数据";
                    }
                    return;
                }

                if (string.IsNullOrEmpty(_originalMomJsonData))
                    return;

                StatusMessage = "🔄 正在重建渲染数据...";

                // 从原始 MomJSON 反序列化
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                var momWall = System.Text.Json.JsonSerializer.Deserialize<MomWall>(_originalMomJsonData, options);
                if (momWall == null || momWall.Outline == null || momWall.Outline.Count == 0)
                    return;

                // 恢复 Face（Face 标记为 [JsonIgnore]，需从 InitialSide 重建）
                foreach (var f in momWall.Features)
                    f.RestoreFaceFromInitialSide();

                // 按序应用变换：翻面 → 原点变换
                if (IsFlipped)
                    momWall.ApplyFlipAroundY();

                if (IsOriginTransformed)
                    momWall.ApplyOriginTransform();

                // 映射渲染数据并注入 HTML
                MapToDObject(momWall);

                var escapedJson = _cachedDObjectJson.Replace("\\", "\\\\").Replace("'", "\\'");
                await ExecuteScriptAsync($"loadWallData('{escapedJson}')");

                // 构建状态消息
                var modes = new List<string>();
                if (IsFlipped) modes.Add("翻面");
                if (IsOriginTransformed) modes.Add("原点变换");
                StatusMessage = $"✅ {string.Join("+", modes)}完成";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重建渲染失败");
                StatusMessage = $"❌ 重建渲染失败: {ex.Message}";
                // 回退 toggle 状态（通过 backing field 避免再次触发 RebuildRenderAsync）
                _isFlipped = false;
                _isOriginTransformed = false;
                OnPropertyChanged(nameof(IsFlipped));
                OnPropertyChanged(nameof(IsOriginTransformed));
                // 尝试恢复原始渲染数据
                if (!string.IsNullOrEmpty(_originalDObjectJson))
                {
                    try
                    {
                        var escaped = _originalDObjectJson.Replace("\\", "\\\\").Replace("'", "\\'");
                        await ExecuteScriptAsync($"loadWallData('{escaped}')");
                    }
                    catch (Exception) { /* 忽略恢复失败 */ }
                }
            }
            finally
            {
                _rebuildLock.Release();
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

                if (message.StartsWith("status:"))
                {
                    var status = message.Length > 7 ? message[7..] : "";
                    if (!string.IsNullOrEmpty(status))
                        StatusMessage = status;
                }
                // 凹槽/MEP槽悬浮交互由 HTML 自管，不通过 postMessage 回传
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "处理 WebMessage 异常");
            }
        }
    }
}
