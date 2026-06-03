using BimWallData.V000;
using CncWallStation.Consts;
using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.Features.MepSlots;
using CncWallStation.Features.Props;
using CncWallStation.MomWallData;
using CncWallStation.Transforms;
using Infrastructure.Maths;
using Newtonsoft.Json;

namespace CncWallStation.VersionMappers
{
    public class BimWallMapperV001 : IBimWallMapper
    {
        public string SupportedVersion => "0.0.0";

        public MomWall Map(string json)
        {
            var dto = JsonConvert.DeserializeObject<BimWallDtoV000>(json);

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            dto.Validate();

            MomWall momWallData = new MomWall(dto.Id, WallElevationConverter.ToVec2Outline(dto.AacWallElevation.Contour), dto.CoreThickness);

            //生成钢柱槽数据SteelColumnGrooves
            ConvertSteelColumnsToFeatures(dto.SteelFrameColumns, momWallData);

            //生成顶板槽数据TopPlateGroove
            ConvertTopPlateToFeature(dto.TopPlate, momWallData);

            //生成XBrace数据XBraceGroove
            ConvertCrossBraceToFeature(dto, momWallData);

            //生成胶水密封槽数据TopPlateGroove
            GenerateGlueSealFeature(momWallData);

            //生成钢筋槽数据
            ConvertRebarSlotToFeature(dto.Rebars, momWallData);

            //生成BendingKey数据
            ConvertBendingKeyToFeature(dto.BendingKeys, momWallData);

            //生成MepCableSlot数据
            ConvertMepCableToFeature(dto.MepCables, momWallData);

            //生成设备盒线槽数据
            ConvertDeviceToFeature(dto.MepDevices,momWallData);

            //生成斜撑数据
            ConvertProppingToFeature(1000f,momWallData);

            //生成窗户数据
            ConvertOpeningToFeature(dto.OpeningHoles,momWallData);

            return momWallData;
        }

        /// <summary>
        /// 将 SteelFrameColumns DTO 列表转换为 Groove Feature 列表
        /// </summary>
        private static void ConvertSteelColumnsToFeatures(
            List<BimSteelFrameColumnDtoV000>? steelFrameColumns, MomWall momWallData)
        {
            if (steelFrameColumns == null || steelFrameColumns.Count == 0)
                return;

            for (int i = 0; i < steelFrameColumns.Count; i++)
            {
                var col = steelFrameColumns[i];

                // ── 1. 空值保护 ──────────────────────────────────────
                if (col.StartPoint == null || col.EndPoint == null)
                {
                    Console.WriteLine(
                        $"[WARN] SteelFrameColumn[{i}] 缺少起点或终点，已跳过");
                    continue;
                }

                //────根据柱子位置添加钢柱槽，顶板槽和底板槽────────────────────

                var ColumnSide = PropertyConverter.DetermineColumnSide(col.StartPoint.X, momWallData);

                switch (ColumnSide)
                {
                    case PropertyConverter.ColumnSide.Left:
                    // ──添加左侧钢柱槽
                    var leftColumnGrooveStartPt = new Vec2(WallConstants.ColumnSteelGrooveSideOffset, momWallData.Width);
                    var leftColumnGrooveEndPt = new Vec2(WallConstants.ColumnSteelGrooveSideOffset, WallConstants.ColumnSteelGrooveBaseOffset);
                    var leftColumnGroove = new Groove(
                    id: $"SteelCol-{i:D1}-{col.Pn ?? "noPn"}",
                    side: MachineSide.Top,
                    startPt: leftColumnGrooveStartPt,
                    endPt: leftColumnGrooveEndPt,
                    width: WallConstants.ColumnSteelGrooveWidth,
                    depth: WallConstants.ColumnSteelGrooveDepth,
                    grooveType: GrooveType.SteelColumn);

                    momWallData.Features.Add(leftColumnGroove);

                    // ──顶板槽局部坐标
                    var columnGrooveLeftTopStartPtY = momWallData.Width - WallConstants.TopBracketGrooveWidth / 2;
                    var columnGrooveLeftTopStartPt = new Vec2(0, columnGrooveLeftTopStartPtY);
                    var columnGrooveLeftTopEndPt = new Vec2(WallConstants.TopBracketGrooveLength, columnGrooveLeftTopStartPtY);

                    // 添加左侧顶板槽
                    var leftTopPlateGroove = new Groove(
                            id: $"TopPlate-Left-{i:D1}",
                            side: MachineSide.Top,
                            startPt: columnGrooveLeftTopStartPt,
                            endPt: columnGrooveLeftTopEndPt,
                            width: WallConstants.TopBracketGrooveWidth,
                            depth: WallConstants.TopBracketGrooveDepth,
                            grooveType: GrooveType.TopBracket);
                    momWallData.Features.Add(leftTopPlateGroove);

                    // ──底板槽局部坐标
                    var columnGrooveLeftBaseStartPtY = WallConstants.BaseBracketGrooveWidth / 2;
                    var columnGrooveLeftBaseStartPt = new Vec2(0, columnGrooveLeftBaseStartPtY);
                    var columnGrooveLeftBaseEndPt = new Vec2(WallConstants.BaseBracketGrooveLength, columnGrooveLeftBaseStartPtY);

                    // 添加左侧底板槽
                    var leftBasePlateGroove = new Groove(
                            id: $"BasePlate-Left-{i:D1}",
                            side: MachineSide.Top,
                            startPt: columnGrooveLeftBaseStartPt,
                            endPt: columnGrooveLeftBaseEndPt,
                            width: WallConstants.BaseBracketGrooveWidth,
                            depth: WallConstants.BaseBracketGrooveDepth,
                            grooveType: GrooveType.BaseBracket);
                    momWallData.Features.Add(leftBasePlateGroove);
                    break;

                    case PropertyConverter.ColumnSide.Right:

                    // ──添加右侧钢柱槽
                    var rightColumnGrooveStartPt = new Vec2(momWallData.Length - WallConstants.ColumnSteelGrooveSideOffset, momWallData.Width);
                    var rightColumnGrooveEndPt = new Vec2(momWallData.Length - WallConstants.ColumnSteelGrooveSideOffset, WallConstants.ColumnSteelGrooveBaseOffset);
                    var rightColumnGroove = new Groove(
                    id: $"SteelCol-{i:D1}-{col.Pn ?? "noPn"}",
                    side: MachineSide.Top,
                    startPt: rightColumnGrooveStartPt,
                    endPt: rightColumnGrooveEndPt,
                    width: WallConstants.ColumnSteelGrooveWidth,
                    depth: WallConstants.ColumnSteelGrooveDepth,
                    grooveType: GrooveType.SteelColumn);

                    momWallData.Features.Add(rightColumnGroove);

                    // ──顶板槽局部坐标
                    var columnGrooveRightTopStartPtY = momWallData.Width - WallConstants.TopBracketGrooveWidth / 2;
                    var columnGrooveTopRightStartPt = new Vec2(momWallData.Length - WallConstants.TopBracketGrooveLength, columnGrooveRightTopStartPtY);
                    var columnGrooveTopRightEndPt = new Vec2(momWallData.Length, columnGrooveRightTopStartPtY);

                    // 添加右侧顶板槽
                    var rightTopPlateGroove = new Groove(
                            id: $"TopPlate-Right-{i:D1}",
                            side: MachineSide.Top,
                            startPt: columnGrooveTopRightStartPt,
                            endPt: columnGrooveTopRightEndPt,
                            width: WallConstants.TopBracketGrooveWidth,
                            depth: WallConstants.TopBracketGrooveDepth,
                            grooveType: GrooveType.TopBracket);
                    momWallData.Features.Add(rightTopPlateGroove);

                    // ──底板槽局部坐标
                    var columnGrooveRightBaseStartPtY = WallConstants.BaseBracketGrooveWidth / 2;
                    var columnGrooveBaseRightStartPt = new Vec2(momWallData.Length - WallConstants.BaseBracketGrooveLength, columnGrooveRightBaseStartPtY);
                    var columnGrooveBaseRightEndPt = new Vec2(momWallData.Length, columnGrooveRightBaseStartPtY);

                    // 添加右侧底板槽
                    var rightBasePlateGroove = new Groove(
                            id: $"BasePlate-Right-{i:D1}",
                            side: MachineSide.Top,
                            startPt: columnGrooveBaseRightStartPt,
                            endPt: columnGrooveBaseRightEndPt,
                            width: WallConstants.BaseBracketGrooveWidth,
                            depth: WallConstants.BaseBracketGrooveDepth,
                            grooveType: GrooveType.BaseBracket);
                    momWallData.Features.Add(rightBasePlateGroove);
                    break;

                    default:
                    break;
                }
            }
        }


        /// <summary>
        /// 将 BimTopPlate DTO 列表转换为 Groove Feature 列表
        /// </summary>
        private static void ConvertTopPlateToFeature(
            List<BimTopPlateDtoV000>? steelFrameColumns, MomWall momWallData)
        {
            // ──顶板槽局部坐标
            var topPlateStartPtY = momWallData.Thickness - WallConstants.TopPlateGrooveWidth / 2;
            var topPlateStartPt = new Vec2(0, topPlateStartPtY);
            var topPlateEndPt = new Vec2(momWallData.Length, topPlateStartPtY);

            // 添加左侧顶板槽
            var topPlateGroove = new Groove(
                    id: $"TopPlate",
                    side: MachineSide.Front,
                    startPt: topPlateStartPt,
                    endPt: topPlateEndPt,
                    width: WallConstants.TopPlateGrooveWidth,
                    depth: WallConstants.TopPlateGrooveDepth,
                    grooveType: GrooveType.TopPlate);

            momWallData.Features.Add(topPlateGroove);
        }


        /// <summary>
        /// 产生胶水密封槽
        /// </summary>
        private static void GenerateGlueSealFeature(MomWall momWallData)
        {
            // ──胶水密封槽局部坐标
            var glueSealStartPtY = WallConstants.GlueSealGrooveWidth / 2;
            var glueSealStartPt = new Vec2(0, glueSealStartPtY);
            var glueSealEndPt = new Vec2(momWallData.Length, glueSealStartPtY);

            // 添加左侧顶板槽
            var glueSealGroove = new Groove(
                    id: $"glueSealGroove",
                    side: MachineSide.Front,
                    startPt: glueSealStartPt,
                    endPt: glueSealEndPt,
                    width: WallConstants.GlueSealGrooveWidth,
                    depth: WallConstants.GlueSealGrooveDepth,
                    grooveType: GrooveType.GlueSeal);

            momWallData.Features.Add(glueSealGroove);
        }


        /// <summary>
        /// 将 BimBendingKey DTO 列表转换为 Hole Feature 列表
        /// </summary>
        private static void ConvertBendingKeyToFeature(
            BimBendingKeyDtoV000? bendingKey, MomWall momWallData)
        {
            if (bendingKey == null) return;
            if (bendingKey.Points == null || bendingKey.Points.Count == 0) return;

            for (int i = 0; i < bendingKey.Points.Count; i++)
            {
                var point = bendingKey.Points[i];

                if (point == null) continue;

                // ── 构造特征 ID ────────────────────────────────────
                // 单点：BendingKey-{Pn}
                // 多点：BendingKey-{Pn}-{序号}
                string id = bendingKey.Points.Count == 1
                    ? $"BendingKey-{bendingKey.Pn}"
                    : $"BendingKey-{bendingKey.Pn}-{i:D2}";

                // ── 中心点坐标 ────────────────────────────────────
                var center = new Vec2((float)point.X, (float)point.Y);

                // ── 添加 BendingKey 特征（底面加工）────────────────────

                var slottedHole = Hole.CreateSlotted(
                id: id,
                side: MachineSide.Back,
                center: center,                                          // 腰孔几何中心
                radius: WallConstants.BendingKeyHoleRadius,              // 端部半圆半径
                depth: WallConstants.BendingKeyHoleDepth,                // 孔加工深度
                slotLength: WallConstants.BendingKeyHoleSlotLength,      // 两圆心距
                slotAngleDeg: WallConstants.BendingKeyHoleSlotAngleDeg,  // 沿 X 轴
                throughHole: false
                );

                momWallData.Features.Add(slottedHole);
            }
        }

        /// <summary>
        /// 将 Mep Device DTO 列表转换为 Pocket Hole Feature 列表
        /// </summary>
        private static void ConvertDeviceToFeature(
            BimMepDeviceDtoV000? device, MomWall momWallData)
        {
            if (device == null) return;
            if (device.Position == null)
            {
                Console.WriteLine($"[WARN] MepDevice [{device.Pn}] 缺少 Position，已跳过");
                return;
            }

            // ── 加工面 ───────────────────────────────────────────────────────
            MachineSide side = device.FrontFace
                ? MachineSide.Top
                : MachineSide.Bottom;

            // ── 特征 ID ──────────────────────────────────────────────────────
            string id = string.IsNullOrWhiteSpace(device.Pn)
                ? $"MepDevice-{device.Position.X:F0}-{device.Position.Y:F0}"
                : $"MepDevice-{device.Pn}";

            // ── 中心点坐标 ───────────────────────────────────────────────────
            var center = new Vec2(
                (float)device.Position.X,
                (float)device.Position.Y);

            // ── 构造 Pocket Feature ──────────────────────────────────────────
            var pocket = new Pocket(
                id: id,
                side: side,
                center: center,
                length: WallConstants.DevicePocketLength,
                width: WallConstants.DevicePocketWidth,
                depth: WallConstants.DevicePocketDepth,
                cornerRadius: WallConstants.DevicePocketCornerRadius);

            momWallData.Features.Add(pocket);
        }


        /// <summary>
        /// 将 Mep Device DTO 列表批量转换为 Pocket Feature 并添加到 MomWall
        /// </summary>
        private static void ConvertDeviceToFeature(
            List<BimMepDeviceDtoV000?>? devices, MomWall momWallData)
        {
            if (devices == null || devices.Count == 0) return;

            foreach (var device in devices)
                ConvertDeviceToFeature(device, momWallData);
        }

        /// <summary>
        /// 将多条 MepCable DTO 批量转换为 MepSlot Feature 并添加到 MomWall
        /// 
        private static void ConvertMepCableToFeature(
            List<BimMepCableDtoV000?> mepCables,
            MomWall momWallData)
        {
            if (mepCables == null || mepCables.Count == 0) return;

            foreach (var mepCable in mepCables)
            {
                MepCableConverter.Convert(mepCable, momWallData);
            }
        }

        //}
        /// <summary>
        /// 将单个 BimRebar DTO 转换为若干 RebarSlot Feature 并加入 MomWall
        /// 
        /// 规则：
        ///   • 方向严格判定：水平 (dy≈0) / 垂直 (dx≈0)，斜向告警跳过
        ///   • 加工面按 Rod 起终点 Z 均值判定：≥ 阈值 → Top，否则 → Bottom
        ///   • 阈值 = 墙厚的一半（位于墙厚中心面）
        /// </summary>
        private static void ConvertRebarSlotToFeature(
            BimRebarDtoV000? rebar, MomWall momWallData)
        {
            // ── 1. 空值保护 ──────────────────────────────────────
            if (rebar == null) return;

            if (rebar.Rods == null || rebar.Rods.Count == 0)
            {
                Console.WriteLine(
                    $"[WARN] Rebar (Pn={rebar.Pn ?? "noPn"}) 缺少 Rods，已跳过");
                return;
            }

            // ── 2. 容差 & 加工面判定阈值 ────────────────────────
            float faceZThreshold = momWallData.Thickness / 2f; // Top/Bottom 分界 Z 值

            // ── 3. 遍历每根 Rod 生成 RebarSlot ───────────────────
            for (int i = 0; i < rebar.Rods.Count; i++)
            {
                var rod = rebar.Rods[i];

                if (rod == null || rod.StartPoint == null || rod.EndPoint == null)
                {
                    Console.WriteLine(
                        $"[WARN] Rebar (Pn={rebar.Pn ?? "noPn"}) Rod[{i}] " +
                        $"起终点缺失，已跳过");
                    continue;
                }

                // ── 3.1 起终点（局部 2D 投影）─────────────────
                var startPos = new Vec2(rod.StartPoint.X, rod.StartPoint.Z);
                var endPos = new Vec2(rod.EndPoint.X, rod.EndPoint.Z);

                // ── 3.2 严格方向判定 ─────────────────────────
                float dx = MathF.Abs(endPos.X - startPos.X);
                float dy = MathF.Abs(endPos.Y - startPos.Y);

                RebarSlotDirection direction;
                if (dy <= WallConstants.DirectionTolerance && dx > WallConstants.DirectionTolerance)
                {
                    direction = RebarSlotDirection.Horizontal;
                }
                else if (dx <= WallConstants.DirectionTolerance && dy > WallConstants.DirectionTolerance)
                {
                    direction = RebarSlotDirection.Vertical;
                }
                else
                {
                    Console.WriteLine(
                        $"[WARN] Rebar (Pn={rebar.Pn ?? "noPn"}) Rod[{i}] " +
                        $"非严格水平/垂直 (dx={dx:F3}, dy={dy:F3})，已跳过");
                    continue;
                }

                // ── 3.3 深度按方向选择 ────────────────────────
                float depth = direction == RebarSlotDirection.Horizontal
                    ? rebar.HorizontalDepth
                    : rebar.VerticalDepth;

                // ── 3.4 加工面按 Z 值判定 ─────────────────────
                float avgZ = (rod.StartPoint.Y + rod.EndPoint.Y) * 0.5f;
                MachineSide side = avgZ >= faceZThreshold
                    ? MachineSide.Top
                    : MachineSide.Bottom;

                // ── 3.5 特征 ID ───────────────────────────────
                string pn = string.IsNullOrWhiteSpace(rebar.Pn) ? "noPn" : rebar.Pn!;
                string id = rebar.Rods.Count == 1
                    ? $"Rebar-{pn}"
                    : $"Rebar-{pn}-{i:D2}";

                // ── 3.6 构造 RebarSlot Feature ────────────────
                var rebarSlot = new RebarSlot(
                    id: id,
                    side: side,
                    startPos: startPos,
                    endPos: endPos,
                    diameter: rebar.Diameter,
                    depth: depth,
                    direction: direction)
                {
                    StartThreading = rod.StartThreading,
                    EndThreading = rod.EndThreading,
                    Pn = rebar.Pn
                };

                momWallData.Features.Add(rebarSlot);
            }
        }

        /// <summary>
        /// 将单个开洞 DTO 转换为 Window Feature 并加入 MomWall
        /// 
        /// 规则：
        ///   • 轮廓点 (X,Y) 投影为局部 2D 坐标
        ///   • 加工面：Top（从墙体顶面下刀）
        ///   • Depth：固定为墙厚（贯穿型开口）
        ///   • LocalPos 自动取轮廓 AABB 左下角（由 Window 构造函数计算）
        /// </summary>
        private static void ConvertOpeningToFeature(
            BimOpeningHoleDtoV000? opening, MomWall momWallData)
        {
            // ── 1. 空值保护 ──────────────────────────────────────
            if (opening == null) return;

            if (opening.Contour == null || opening.Contour.Count < 3)
            {
                Console.WriteLine(
                    $"[WARN] Opening (Uuid={opening.Uuid ?? "noUuid"}) " +
                    $"轮廓点不足 3 个，无法构成多边形，已跳过");
                return;
            }

            // ── 2. 轮廓点 (X,Y) 投影为 Vec2 ─────────────────────
            var contour = new List<Vec2>(opening.Contour.Count);
            foreach (var p in opening.Contour)
            {
                if (p == null) continue;
                contour.Add(new Vec2((float)p.X, (float)p.Y));
            }

            if (contour.Count < 3)
            {
                Console.WriteLine(
                    $"[WARN] Opening (Uuid={opening.Uuid ?? "noUuid"}) " +
                    $"有效轮廓点不足 3 个，已跳过");
                return;
            }

            // ── 3. 特征 ID ──────────────────────────────────────
            string id = string.IsNullOrWhiteSpace(opening.Uuid)
                ? $"Window-{momWallData.Features.Count(f => f.Type == FeatureType.Window):D2}"
                : $"Window-{opening.Uuid}";

            // ── 4. 加工面 + 深度 ────────────────────────────────
            MachineSide side = MachineSide.Top;
            float depth = momWallData.Thickness;   // 默认贯穿墙厚

            // ── 5. 构造 Window Feature ──────────────────────────
            var window = new Window(
                id: id,
                side: side,
                contour: contour,
                depth: depth);

            momWallData.Features.Add(window);
        }

        /// <summary>
        /// 将开洞 DTO 列表批量转换为 Window Feature 并加入 MomWall
        /// </summary>
        private static void ConvertOpeningToFeature(
            List<BimOpeningHoleDtoV000>? openings, MomWall momWallData)
        {
            if (openings == null || openings.Count == 0) return;

            foreach (var opening in openings)
                ConvertOpeningToFeature(opening, momWallData);
        }


        /// <summary>
        /// 将多个斜撑 DTO 批量转换为 Propping Feature 并添加到 MomWall
        /// 
        private static void ConvertProppingToFeature(float centerX,
            MomWall momWallData)
        {
            ProppingConverter.Convert(
                centerX: centerX,
                momWallData: momWallData,
                id: "Propping-01");
        }

        /// <summary>
        /// 当墙类型为 CrossBraceWall 时，生成 X 形斜撑钢槽
        /// 
        /// 端点规则（基于钢柱中心线与墙顶/底边的交点）：
        ///   • 左上：左侧钢柱中心 X，墙顶 Y + 4mm（向外）
        ///   • 左下：左侧钢柱中心 X，墙底 Y - 6mm（向外）
        ///   • 右上：右侧钢柱中心 X，墙顶 Y + 4mm
        ///   • 右下：右侧钢柱中心 X，墙底 Y - 6mm
        /// 
        /// 槽路径：
        ///   ① 左下 → 右上
        ///   ② 左上 → 右下
        /// </summary>
        private static void ConvertCrossBraceToFeature(
            BimWallDtoV000? dto, MomWall momWallData)
        {
            // ── 1. 仅 CrossBraceWall 才生成 ──────────────────────
            if (dto == null) return;
            if (!string.Equals(dto.WallType, "CrossBraceWall",
                               StringComparison.OrdinalIgnoreCase))
                return;

            // ── 2. 必须有钢柱数据 ────────────────────────────────
            if (dto.SteelFrameColumns == null || dto.SteelFrameColumns.Count < 2)
            {
                Console.WriteLine(
                    $"[WARN] CrossBraceWall (Pn={dto.Pn ?? "noPn"}) " +
                    $"钢柱数量 < 2，无法定位斜撑端点，已跳过");
                return;
            }

            // ── 3. 找出最左 / 最右钢柱中心 X ─────────────────────
            float leftColumnX = float.MaxValue;
            float rightColumnX = float.MinValue;

            foreach (var col in dto.SteelFrameColumns)
            {
                if (col == null) continue;
                float cx = col.StartPoint.X;   // ← 取钢柱中心 X
                if (cx < leftColumnX) leftColumnX = cx;
                if (cx > rightColumnX) rightColumnX = cx;
            }

            if (leftColumnX >= rightColumnX)
            {
                Console.WriteLine(
                    $"[WARN] CrossBraceWall (Pn={dto.Pn ?? "noPn"}) " +
                    $"无法识别有效左右钢柱中心，已跳过");
                return;
            }

            // ── 4. 墙体上下边 Y ─────────────────────────────────
            float wallBottomY = 0f;
            float wallTopY = momWallData.Width;


            // ── 6. 计算 4 个端点 ────────────────────────────────
            Vec2 leftTop = new Vec2(leftColumnX, wallTopY + WallConstants.XBraceTopOffset);
            Vec2 leftBottom = new Vec2(leftColumnX, wallBottomY - WallConstants.XBraceBottomOffset);
            Vec2 rightTop = new Vec2(rightColumnX, wallTopY + WallConstants.XBraceTopOffset);
            Vec2 rightBottom = new Vec2(rightColumnX, wallBottomY - WallConstants.XBraceBottomOffset);


            string pnPrefix = string.IsNullOrWhiteSpace(dto.Pn) ? "Wall" : dto.Pn;

            // ── 8. 生成 2 条斜撑槽 ──────────────────────────────
            // 斜撑 ①：左下 → 右上
            momWallData.Features.Add(new Groove(
                id: $"XBrace-{pnPrefix}-01",
                side: MachineSide.Top,
                startPt: leftBottom,
                endPt: rightTop,
                width: WallConstants.XBraceGrooveWidth,
                depth: WallConstants.XBraceGrooveDepth,
                grooveType: GrooveType.XBraceSteel));

            // 斜撑 ②：左上 → 右下
            momWallData.Features.Add(new Groove(
                id: $"XBrace-{pnPrefix}-02",
                side: MachineSide.Top,
                startPt: leftTop,
                endPt: rightBottom,
                width: WallConstants.XBraceGrooveWidth,
                depth: WallConstants.XBraceGrooveDepth,
                grooveType: GrooveType.XBraceSteel));
        }

    }
}
