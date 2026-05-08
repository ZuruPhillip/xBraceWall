using CncWallStation.Commands;
using CncWallStation.Features;
using CncWallStation.MomWallData;
using CncWallStation.VersionMappers;
using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Maths;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CncWallStation.ViewModels
{
    public partial class ControllerPageViewModel : ObservableObject
    {
        private readonly BimWallMapperFactory _factory = new();
        private readonly ILogger<ControllerPageViewModel> _logger;

        public RelayCommand WallRotationTestCommand { get; }
        public RelayCommand WallDataGenerateCommand { get; }
        public ControllerPageViewModel(ILogger<ControllerPageViewModel> logger)
        {
            _logger = logger;

            WallRotationTestCommand = new RelayCommand(
                execute: _ => ExecuteLoadRender()
            );

            WallDataGenerateCommand = new RelayCommand(
                execute: _ => LoadFromFile()
            );
        }

        private void ExecuteLoadRender()
        {
            // ══════════════════════════════════════════
            // ① 定义 L 形墙体轮廓（单位 mm）
            // ══════════════════════════════════════════
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

            // ══════════════════════════════════════════
            // ② 链式添加加工特征
            // ══════════════════════════════════════════
            wall
                .AddGroove("G-001", MachineSide.Top,
                           new Vec2(100, 50), new Vec2(150, 50),
                           width: 18f, depth: 10f);

            //.AddHole("H-001", MachineSide.Top,
            //         center: new Vec2(30, 150), radius: 4f,
            //         depth: 18f, throughHole: true)

            //.AddHole("H-002", MachineSide.Top,
            //         center: new Vec2(120, 150), radius: 4f,
            //         depth: 18f, throughHole: true)

            //.AddPocket("P-001", MachineSide.Bottom,
            //           center: new Vec2(75, 50), width: 30f,
            //           height: 10f, depth: 5f, cornerRadius: 2f)

            //.AddHole("H-003", MachineSide.Front,
            //         center: new Vec2(75, 0), radius: 5f,
            //         depth: 10f);

            // ── 添加 MepSlot（链式构造路径）──────────────────────────

            // ① L 形线槽（水平段 + 垂直段，深度均一）
            wall.AddMepSlot("MS-001", MachineSide.Top, width: 20f)
                .AddLine(new Vec2(50, 50), new Vec2(300, 50), depth: 12f)
                .LineTo(new Vec2(300, 300), depth: 12f);

            // ② S 形线槽（直线 + 圆弧 + 直线，深度渐变）
            wall.AddMepSlot("MS-002", MachineSide.Top, width: 15f)
                .AddLine(new Vec2(350, 50), new Vec2(400, 50), depth: 8f)
                .AddArc(center: new Vec2(400, 100),
                         radius: 50f,
                         startAngleDeg: 270f,
                         endAngleDeg: 90f,
                         depth: 10f,
                         isClockwise: false)
                .LineTo(new Vec2(350, 200), depth: 12f);

            // ③ 三点圆弧线槽
            wall.AddMepSlot("MS-003", MachineSide.Front, width: 25f)
                .AddArcByThreePoints(
                    p1: new Vec2(100, 0),
                    pMid: new Vec2(200, 30),
                    p3: new Vec2(300, 0),
                    depth: 15f)
                .LineTo(new Vec2(500, 0), depth: 15f);


            // ── 对称槽 ────────────────────────────────────────────────

            // 方式①：通用方法 + 指定类型
            wall.AddGroove("G-001", MachineSide.Top,
                startPt: new Vec2(0, 100),
                endPt: new Vec2(600, 100),
                width: 40f,
                depth: 12f,
                grooveType: GrooveType.SteelColumn);

            // 方式②：快捷方法
            wall.AddTopPlateGroove("G-002", MachineSide.Top,
                startPt: new Vec2(0, 0),
                endPt: new Vec2(600, 0),
                width: 38f,
                depth: 18f);

            // ── 非对称槽 ──────────────────────────────────────────────

            // 方式①：通用方法 + 指定类型
            wall.AddAsymmetricGroove("G-003", MachineSide.Top,
                startPt: new Vec2(0, 200),
                endPt: new Vec2(600, 200),
                leftWidth: 30f,   // 中心线左侧 30mm
                rightWidth: 10f,   // 中心线右侧 10mm
                depth: 15f,
                grooveType: GrooveType.XBraceSteel);

            // 方式②：快捷方法（斜撑钢槽默认非对称）
            wall.AddXBraceSteelGroove("G-004", MachineSide.Top,
                startPt: new Vec2(100, 300),
                endPt: new Vec2(500, 100),
                leftWidth: 25f,
                rightWidth: 15f,
                depth: 12f);

            // 手动覆盖实际尺寸（如有加工公差）
            wall.ActualLength = 1398f;
            wall.ActualThickness = 2693f;

            // 手动设置基准点（如对齐钢柱中心）
            wall.PivotPoint = new Vec3(-50f, 148f, 0f);

            // 重置
            wall.ResetActualDimensions();   // 恢复跟随计算值
            wall.ResetPivotPoint();         // 恢复左下角

            // ── 查询 ──────────────────────────────────────────────────
            foreach (var f in wall.Features.OfType<Groove>())
            {
                Console.WriteLine(f.GetInfo());

                // 打印四角坐标
                var (p0, p1, p2, p3) = f.GetCorners();
                Console.WriteLine($"  角点: P0={p0} P1={p1} P2={p2} P3={p3}");
            }

            // ══════════════════════════════════════════
            // ③ 初始状态
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【初始状态】");
            wall.Print();
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");
            // ══════════════════════════════════════════
            // ④ 第一面加工（顶面 Top）
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【第一面加工 - 顶面 Top 特征】");
            foreach (var f in wall.GetFeaturesByCurrentSide(MachineSide.Top))
                _logger.LogInformation($"  {f.GetInfo()}");

            // ══════════════════════════════════════════
            // ⑤ 翻面（绕 X 轴，Top → Bottom）
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【执行翻面：绕 X 轴（Top -> Bottom）】");
            wall.Flip(FlipAxis.AroundY);
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            _logger.LogInformation("\n【第二面加工 - 翻面后顶面（原 Bottom）特征】");
            foreach (var f in wall.GetFeaturesByCurrentSide(MachineSide.Top))
                _logger.LogInformation($"  {f.GetInfo()}");

            // ══════════════════════════════════════════
            // ⑥ CNC 旋转定位（绕 Z 轴旋转 90°）
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【CNC 旋转：绕 Z 轴 90°】");
            wall.Rotate(Vec3.UnitZ, 90f, pivot: new Vec3(50, 0, 0));
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            _logger.LogInformation($"\n旋转后顶点[0]: {wall.GetWorldVertices()[0]}");

            // ══════════════════════════════════════════
            // ⑦ 平移到加工台坐标
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【CNC 平移：移到加工台坐标 (500, 200, 0)】");
            wall.Translate(new Vec3(500f, 200f, 0f));

            var (bmin, bmax) = wall.GetBoundingBox();
            _logger.LogInformation($"包围盒 Min: {bmin}");
            _logger.LogInformation($"包围盒 Max: {bmax}");
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");

            // ══════════════════════════════════════════
            // ⑧ 输出特征世界坐标（CNC 路径生成用）
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【特征世界坐标（CNC 路径）】");
            foreach (var (feature, worldPos) in wall.GetFeaturesWorldPos())
                _logger.LogInformation($"  {feature.Id,-6} → {worldPos}");

            // ══════════════════════════════════════════
            // ⑨ 撤销平移
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【撤销平移】");
            wall.UndoTransform();
            _logger.LogInformation($"撤销后顶点[0]: {wall.GetWorldVertices()[0]}");
            _logger.LogInformation($"剩余可撤销变换步数: {wall.UndoTransformSteps}");
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");
            // ══════════════════════════════════════════
            // ⑩ 撤销翻面
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【撤销翻面】");
            wall.UndoFlip();
            wall.PrintWorldCoordinates("-------------当前世界坐标---------------");
            // ══════════════════════════════════════════
            // ⑪ SLERP 旋转动画插值（5 帧）
            // ══════════════════════════════════════════
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


        /// <summary>
        /// 从文件路径读取并转换为 MomWallData
        /// </summary>
        public MomWall LoadFromFile()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\Resources\\TestMjsons\\Wall1.mjson");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在：{filePath}");

            string json = File.ReadAllText(filePath, Encoding.UTF8);

            return ConvertToMom(json);
        }

        /// <summary>
        /// 从文件路径异步读取并转换为 MomWallData
        /// </summary>
        public async Task<MomWall> LoadFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在：{filePath}");

            string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            return ConvertToMom(json);
        }

        private MomWall ConvertToMom(string json)
        {
            // 1. 解析版本
            string version = BimDataVersionResolver.ResolveVersion(json);
            _logger.LogInformation($"检测到 BimData 版本：v{version}");

            // 2. 获取对应 Mapper
            IBimWallMapper mapper = _factory.GetMapper(version);

            // 3. 转换
            var momWall = mapper.Map(json);

            SaveMomWallToJson(momWall);
            // ══════════════════════════════════════════
            // ③ 初始状态
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【初始状态】");
            momWall.Print();
            momWall.PrintWorldCoordinates("-------------当前世界坐标---------------");
            // ══════════════════════════════════════════
            // ④ 第一面加工（顶面 Top）
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【第一面加工 - 顶面 Top 特征】");
            foreach (var f in momWall.GetFeaturesByCurrentSide(MachineSide.Top))
                _logger.LogInformation($"  {f.GetInfo()}");

            return momWall;
        }

        /// <summary>
        /// 将 MomWall 对象序列化为 JSON 并保存到本地
        /// 文件路径：./output/momwall/{wallId}_{timestamp}.json
        /// </summary>
        private void SaveMomWallToJson(MomWall momWall)
        {
            try
            {
                // ── 1. 构造输出目录 ────────────────────────────────────
                string outputDir = Path.Combine(
                    AppContext.BaseDirectory, "output", "momwall");

                Directory.CreateDirectory(outputDir); // 目录不存在则自动创建

                // ── 2. 构造文件名（墙 ID + 时间戳，避免覆盖）────────────
                string fileName = $"momwall_{momWall.Id}.json";
                string filePath = Path.Combine(outputDir, fileName);

                // ── 3. 序列化 ─────────────────────────────────────────
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,               // 格式化缩进
                    Encoder = JavaScriptEncoder   // 保留中文，不转义
                                         .UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    // ── 枚举序列化为字符串 ──────────────────────────────────
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };
                

                string momWallJson = JsonSerializer.Serialize(momWall, options);

                // ── 4. 写入文件 ────────────────────────────────────────
                File.WriteAllText(filePath, momWallJson, Encoding.UTF8);

                _logger.LogInformation(
                    $"MomWall 已保存：{filePath}");
            }
            catch (Exception ex)
            {
                // 保存失败不应中断主流程，仅记录警告
                _logger.LogWarning(
                    $"MomWall JSON 保存失败（Wall Id={momWall?.Id}）：{ex.Message}");
            }
        }
    }
}
