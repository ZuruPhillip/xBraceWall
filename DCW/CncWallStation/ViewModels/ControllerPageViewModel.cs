using CncWallStation.Commands;
using CncWallStation.Features;
using CncWallStation.MomWallData;
using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Maths;
using Microsoft.Extensions.Logging;

namespace CncWallStation.ViewModels
{
    public partial class ControllerPageViewModel : ObservableObject
    {
        private readonly ILogger<ControllerPageViewModel> _logger;

        public RelayCommand WallRotationTestCommand { get; }

        public ControllerPageViewModel(ILogger<ControllerPageViewModel> logger)
        {
            _logger = logger;

            WallRotationTestCommand = new RelayCommand(
                execute: _ => ExecuteLoadRender()
            );
        }

        private void ExecuteLoadRender()
        {
            // ══════════════════════════════════════════
            // ① 定义 L 形墙体轮廓（单位 mm）
            //
            //   Y
            //   ↑  (0,200)──(150,200)
            //   │     │           │
            //   │  (0,100)─(100,100)
            //   │             │
            //   │          (100,0)─(150,0)
            //   └────────────────────────→ X
            // ══════════════════════════════════════════
            var outline = new Vec2[]
            {
                new Vec2(  0,   0),
                new Vec2(150,   0),
                new Vec2(150, 200),
                new Vec2(  0, 200),
                new Vec2(  0, 100),
                new Vec2(100, 100),
                new Vec2(100,   0),
            };

            var wall = new Wall("W-001", outline,
                                thickness: 18f,
                                baseElevation: 0f,
                                material: "多层板");

            // ══════════════════════════════════════════
            // ② 链式添加加工特征
            // ══════════════════════════════════════════
            wall
                .AddGroove("G-001", MachineSide.Top,
                           new Vec2(10, 100), new Vec2(140, 100),
                           width: 18f, depth: 10f)

                .AddHole("H-001", MachineSide.Top,
                         center: new Vec2(30, 150), radius: 4f,
                         depth: 18f, throughHole: true)

                .AddHole("H-002", MachineSide.Top,
                         center: new Vec2(120, 150), radius: 4f,
                         depth: 18f, throughHole: true)

                .AddPocket("P-001", MachineSide.Bottom,
                           center: new Vec2(75, 50), width: 30f,
                           height: 10f, depth: 5f, cornerRadius: 2f)

                .AddHole("H-003", MachineSide.Front,
                         center: new Vec2(75, 0), radius: 5f,
                         depth: 10f);

            // ══════════════════════════════════════════
            // ③ 初始状态
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【初始状态】");
            wall.Print();
            wall.PrintFaceReport();

            // ══════════════════════════════════════════
            // ④ 第一面加工（顶面 Top）
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【第一面加工 - 顶面 Top 特征】");
            foreach (var f in wall.GetFeaturesByCurrentSide(MachineSide.Top))
                _logger.LogInformation($"  {f.GetInfo()}");

            // ══════════════════════════════════════════
            // ⑤ 翻面（绕 X 轴，Top → Bottom）
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【执行翻面：绕 X 轴（Top ↔ Bottom）】");
            wall.Flip(FlipAxis.AroundX);
            wall.PrintFaceReport();

            _logger.LogInformation("\n【第二面加工 - 翻面后顶面（原 Bottom）特征】");
            foreach (var f in wall.GetFeaturesByCurrentSide(MachineSide.Top))
                _logger.LogInformation($"  {f.GetInfo()}");

            // ══════════════════════════════════════════
            // ⑥ CNC 旋转定位（绕 Z 轴旋转 90°）
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【CNC 旋转：绕 Z 轴 90°】");
            wall.Rotate(Vec3.UnitZ, 90f, pivot: new Vec3(75, 100, 9));
            wall.PrintFaceReport();

            _logger.LogInformation($"\n旋转后顶点[0]: {wall.GetWorldVertices()[0]}");

            // ══════════════════════════════════════════
            // ⑦ 平移到加工台坐标
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【CNC 平移：移到加工台坐标 (500, 200, 0)】");
            wall.Translate(new Vec3(500f, 200f, 0f));

            var (bmin, bmax) = wall.GetBoundingBox();
            _logger.LogInformation($"包围盒 Min: {bmin}");
            _logger.LogInformation($"包围盒 Max: {bmax}");

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

            // ══════════════════════════════════════════
            // ⑩ 撤销翻面
            // ══════════════════════════════════════════
            _logger.LogInformation("\n【撤销翻面】");
            wall.UndoFlip();
            wall.PrintFaceReport();

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

            _logger.LogError("\n完成。");
        }

    }
}
