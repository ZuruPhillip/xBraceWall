using CncWallStation.Commands;
using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.MomWallData;
using CncWallStation.Services.OpcUa;
using CncWallStation.VersionMappers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Maths;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

// 解决项目自定义 RelayCommand 与 CommunityToolkit.Mvvm 生成的 RelayCommand 歧义
using CncRelayCommand = CncWallStation.Commands.RelayCommand;

namespace CncWallStation.ViewModels
{
    public partial class ControllerPageViewModel : ObservableObject
    {
        private readonly BimWallMapperFactory _factory = new();
        private readonly ILogger<ControllerPageViewModel> _logger;
        private readonly IOpcUaService _opcUaService;

        // ══════════════════════════════════════════
        //  原有测试命令
        // ══════════════════════════════════════════

        public CncRelayCommand WallRotationTestCommand { get; }
        public CncRelayCommand WallDataGenerateCommand { get; }

        // ══════════════════════════════════════════
        //  OPC 节点监控
        // ══════════════════════════════════════════

        public ObservableCollection<OpcNodeConfig> OpcMonitoredNodes { get; } = new();

        [ObservableProperty]
        private OpcNodeConfig? _selectedOpcMonitoredNode;

        public ControllerPageViewModel(
            ILogger<ControllerPageViewModel> logger,
            IOpcUaService opcUaService)
        {
            _logger = logger;
            _opcUaService = opcUaService;

            WallRotationTestCommand = new CncRelayCommand(
                execute: _ => ExecuteLoadRender()
            );

            WallDataGenerateCommand = new CncRelayCommand(
                execute: _ => LoadFromFile()
            );
        }

        // ══════════════════════════════════════════
        //  OPC 命令
        // ══════════════════════════════════════════

        /// <summary>
        /// 从配置文件加载节点列表到监控面板
        /// </summary>
        [RelayCommand]
        private async Task LoadOpcNodesAsync()
        {
            try
            {
                var nodesFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CncWallStation", "opc_nodes.json");

                OpcMonitoredNodes.Clear();

                if (File.Exists(nodesFilePath))
                {
                    var json = await File.ReadAllTextAsync(nodesFilePath);
                    var nodes = JsonSerializer.Deserialize<List<OpcNodeConfig>>(json);
                    if (nodes != null)
                    {
                        foreach (var node in nodes)
                            OpcMonitoredNodes.Add(node);
                    }
                }

                _logger.LogInformation("已加载 {Count} 个节点到监控面板", OpcMonitoredNodes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载 OPC 节点到监控面板失败");
            }
        }

        /// <summary>
        /// 刷新所有节点的当前值（批量读取）
        /// </summary>
        [RelayCommand]
        private async Task RefreshOpcValuesAsync()
        {
            if (!_opcUaService.IsConnected)
            {
                _logger.LogWarning("OPC 未连接，无法刷新节点值");
                return;
            }

            try
            {
                var nodeIds = OpcMonitoredNodes.Select(n => n.NodeId).ToList();
                if (nodeIds.Count == 0) return;

                var values = await _opcUaService.ReadNodesAsync(nodeIds);
                _logger.LogInformation("刷新完成: {Count} 个节点", values.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新 OPC 节点值失败");
            }
        }

        /// <summary>
        /// 订阅所有节点
        /// </summary>
        [RelayCommand]
        private async Task SubscribeAllNodesAsync()
        {
            if (!_opcUaService.IsConnected)
            {
                _logger.LogWarning("OPC 未连接，无法订阅节点");
                return;
            }

            try
            {
                var nodes = OpcMonitoredNodes.ToList();
                if (nodes.Count == 0)
                {
                    _logger.LogWarning("没有可订阅的节点，请先加载节点");
                    return;
                }

                await _opcUaService.SubscribeNodesAsync(nodes);
                _logger.LogInformation("已订阅 {Count} 个节点", nodes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅节点失败");
            }
        }

        // ══════════════════════════════════════════
        //  原有测试方法
        // ══════════════════════════════════════════

        private void ExecuteLoadRender()
        {
            var outline = new Vec2[]
            {
                new Vec2(100,   0),
                new Vec2(150,   0),
                new Vec2(150, 200),
                new Vec2(  0, 200),
                new Vec2(  0, 100),
                new Vec2(100, 100),
            };

            var wall = new MomWall("W-001", outline,
                                thickness: 18f,
                                baseElevation: 0f,
                                material: "多层板");

            wall
                .AddGroove("G-001", MachineSide.Top,
                           new Vec2(100, 50), new Vec2(150, 50),
                           width: 18f, depth: 10f);

            wall.AddMepSlot("MS-001", MachineSide.Top, width: 20f)
                .AddLine(new Vec2(50, 50), new Vec2(300, 50), depth: 12f)
                .LineTo(new Vec2(300, 300), depth: 12f);

            wall.AddMepSlot("MS-002", MachineSide.Top, width: 15f)
                .AddLine(new Vec2(350, 50), new Vec2(400, 50), depth: 8f)
                .AddArc(center: new Vec2(400, 100),
                         radius: 50f,
                         startAngleDeg: 270f,
                         endAngleDeg: 90f,
                         depth: 10f,
                         isClockwise: false)
                .LineTo(new Vec2(350, 200), depth: 12f);

            wall.AddMepSlot("MS-003", MachineSide.Front, width: 25f)
                .AddArcByThreePoints(
                    p1: new Vec2(100, 0),
                    pMid: new Vec2(200, 30),
                    p3: new Vec2(300, 0),
                    depth: 15f)
                .LineTo(new Vec2(500, 0), depth: 15f);

            wall.AddGroove("G-001", MachineSide.Top,
                startPt: new Vec2(0, 100),
                endPt: new Vec2(600, 100),
                width: 40f,
                depth: 12f,
                grooveType: GrooveType.SteelColumn);

            wall.AddTopPlateGroove("G-002", MachineSide.Top,
                startPt: new Vec2(0, 0),
                endPt: new Vec2(600, 0),
                width: 38f,
                depth: 18f);

            wall.AddAsymmetricGroove("G-003", MachineSide.Top,
                startPt: new Vec2(0, 200),
                endPt: new Vec2(600, 200),
                leftWidth: 30f,
                rightWidth: 10f,
                depth: 15f,
                grooveType: GrooveType.XBraceSteel);

            wall.AddXBraceSteelGroove("G-004", MachineSide.Top,
                startPt: new Vec2(100, 300),
                endPt: new Vec2(500, 100),
                leftWidth: 25f,
                rightWidth: 15f,
                depth: 12f);

            wall.ActualLength = 1398f;
            wall.ActualThickness = 2693f;
            wall.PivotPoint = new Vec3(-50f, 148f, 0f);
            wall.ResetActualDimensions();
            wall.ResetPivotPoint();

            foreach (var f in wall.Features.OfType<Groove>())
            {
                Console.WriteLine(f.GetInfo());
                var (p0, p1, p2, p3) = f.GetCorners();
                Console.WriteLine($"  角点: P0={p0} P1={p1} P2={p2} P3={p3}");
            }

            _logger.LogInformation("\n【初始状态】");
            wall.Print();
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            _logger.LogInformation("\n【第一面加工 - 顶面 Top 特征】");
            foreach (var f in wall.GetFeaturesByCurrentSide(MachineSide.Top))
                _logger.LogInformation($"  {f.GetInfo()}");

            _logger.LogInformation("\n【执行翻面：绕 X 轴（Top -> Bottom）】");
            wall.Flip(FlipAxis.AroundY);
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            _logger.LogInformation("\n【第二面加工 - 翻面后顶面（原 Bottom）特征】");
            foreach (var f in wall.GetFeaturesByCurrentSide(MachineSide.Top))
                _logger.LogInformation($"  {f.GetInfo()}");

            _logger.LogInformation("\n【CNC 旋转：绕 Z 轴 90°】");
            wall.Rotate(Vec3.UnitZ, 90f, pivot: new Vec3(50, 0, 0));
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");
            _logger.LogInformation($"\n旋转后顶点[0]: {wall.GetWorldVertices()[0]}");

            _logger.LogInformation("\n【CNC 平移：移到加工台坐标 (500, 200, 0)】");
            wall.Translate(new Vec3(500f, 200f, 0f));

            var (bmin, bmax) = wall.GetBoundingBox();
            _logger.LogInformation($"包围盒 Min: {bmin}");
            _logger.LogInformation($"包围盒 Max: {bmax}");
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            _logger.LogInformation("\n【特征世界坐标（CNC 路径）】");
            foreach (var (feature, worldPos) in wall.GetFeaturesWorldPos())
                _logger.LogInformation($"  {feature.Id,-6} → {worldPos}");

            _logger.LogInformation("\n【撤销平移】");
            wall.UndoTransform();
            _logger.LogInformation($"撤销后顶点[0]: {wall.GetWorldVertices()[0]}");
            _logger.LogInformation($"剩余可撤销变换步数: {wall.UndoTransformSteps}");
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            _logger.LogInformation("\n【撤销翻面】");
            wall.UndoFlip();
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            _logger.LogInformation("\n【SLERP 旋转插值（5 帧）】");
            var qStart = Quaternion.Identity;
            var qEnd = Quaternion.FromAxisAngle(Vec3.UnitY, MathF.PI / 2f);
            for (int i = 0; i <= 5; i++)
            {
                float t = i / 5f;
                var q = Quaternion.Slerp(qStart, qEnd, t);
                _logger.LogInformation($"  t={t:F1} → {q}");
            }

            _logger.LogInformation("\n完成。");
        }

        public MomWall LoadFromFile()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\Resources\\TestMjsons\\Wall3.mjson");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在：{filePath}");

            string json = File.ReadAllText(filePath, Encoding.UTF8);

            return ConvertToMom(json);
        }

        public async Task<MomWall> LoadFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在：{filePath}");

            string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            return ConvertToMom(json);
        }

        private MomWall ConvertToMom(string json)
        {
            string version = BimDataVersionResolver.ResolveVersion(json);
            _logger.LogInformation($"检测到 BimData 版本：v{version}");

            IBimWallMapper mapper = _factory.GetMapper(version);
            var momWall = mapper.Map(json);

            SaveMomWallToJson(momWall);

            _logger.LogInformation("\n【初始状态】");
            momWall.Print();
            momWall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            _logger.LogInformation("\n【第一面加工 - 顶面 Top 特征】");
            foreach (var f in momWall.GetFeaturesByCurrentSide(MachineSide.Top))
                _logger.LogInformation($"  {f.GetInfo()}");

            return momWall;
        }

        private void SaveMomWallToJson(MomWall momWall)
        {
            try
            {
                string outputDir = Path.Combine(
                    AppContext.BaseDirectory, "output", "momwall");
                Directory.CreateDirectory(outputDir);

                string fileName = $"momwall_{momWall.Id}.json";
                string filePath = Path.Combine(outputDir, fileName);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };

                string momWallJson = JsonSerializer.Serialize(momWall, options);
                File.WriteAllText(filePath, momWallJson, Encoding.UTF8);

                _logger.LogInformation($"MomWall 已保存：{filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"MomWall JSON 保存失败（Wall Id={momWall?.Id}）：{ex.Message}");
            }
        }
    }
}
