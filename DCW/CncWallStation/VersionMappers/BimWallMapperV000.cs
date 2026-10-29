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
    public class BimWallMapperV000 : IBimWallMapper
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

            //生成剪力钉孔数据
            ConvertStudsToFeature(dto.TopPlate, momWallData);

            //生成XBrace数据XBraceGroove
            ConvertCrossBraceToFeature(dto, momWallData);

            //生成胶水密封槽数据TopPlateGroove
            //GenerateGlueSealFeature(momWallData);

            //生成钢筋槽数据
            ConvertRebarSlotToFeature(dto.Rebars, momWallData);

            //生成BendingKey数据
            ConvertBendingKeyToFeature(dto.BendingKeys, momWallData);

            //生成MepCableSlot数据
            ConvertMepCableToFeature(dto.MepCables, momWallData);

            //生成设备盒线槽数据
            ConvertDeviceToFeature(dto.MepDevices,momWallData);

            //生成斜撑数据
            ConvertProppingToFeature(2000f,momWallData);

            //生成窗户数据
            ConvertOpeningToFeature(dto.OpeningHoles,momWallData);

            return momWallData;
        }

        #region 钢柱槽
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
                    // ──左侧钢柱槽：贴墙左边，分下/上两段
                    // 下段：墙底 → 高度 1000mm，槽宽 103，槽深 103
                    var leftLowerGrooveCenterX = WallConstants.ColumnSteelGrooveLowerWidth / 2f;
                    var leftLowerStartPt = new Vec2(leftLowerGrooveCenterX, 0f);
                    var leftLowerEndPt = new Vec2(leftLowerGrooveCenterX, WallConstants.ColumnSteelGrooveBaseOffset);
                    var leftColumnGrooveLower = new Groove(
                    id: $"SteelCol-{i:D1}-{col.Pn ?? "noPn"}-Lower",
                    side: MachineSide.Top,
                    startPt: leftLowerStartPt,
                    endPt: leftLowerEndPt,
                    width: WallConstants.ColumnSteelGrooveLowerWidth,
                    depth: WallConstants.ColumnSteelGrooveDepth,
                    grooveType: GrooveType.SteelColumn);

                    momWallData.Features.Add(leftColumnGrooveLower);

                    // 上段：高度 1000mm → 墙顶，槽宽 178，槽深 103
                    var leftUpperGrooveCenterX = WallConstants.ColumnSteelGrooveUpperWidth / 2f;
                    var leftUpperStartPt = new Vec2(leftUpperGrooveCenterX, WallConstants.ColumnSteelGrooveBaseOffset);
                    var leftUpperEndPt = new Vec2(leftUpperGrooveCenterX, momWallData.Width);
                    var leftColumnGrooveUpper = new Groove(
                    id: $"SteelCol-{i:D1}-{col.Pn ?? "noPn"}-Upper",
                    side: MachineSide.Top,
                    startPt: leftUpperStartPt,
                    endPt: leftUpperEndPt,
                    width: WallConstants.ColumnSteelGrooveUpperWidth,
                    depth: WallConstants.ColumnSteelGrooveDepth,
                    grooveType: GrooveType.SteelColumn);

                    momWallData.Features.Add(leftColumnGrooveUpper);

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

                    // ──右侧钢柱槽：贴墙右边，分下/上两段
                    // 下段：墙底 → 高度 1000mm，槽宽 103，槽深 103
                    var rightLowerGrooveCenterX = momWallData.Length - WallConstants.ColumnSteelGrooveLowerWidth / 2f;
                    var rightLowerStartPt = new Vec2(rightLowerGrooveCenterX, 0f);
                    var rightLowerEndPt = new Vec2(rightLowerGrooveCenterX, WallConstants.ColumnSteelGrooveBaseOffset);
                    var rightColumnGrooveLower = new Groove(
                    id: $"SteelCol-{i:D1}-{col.Pn ?? "noPn"}-Lower",
                    side: MachineSide.Top,
                    startPt: rightLowerStartPt,
                    endPt: rightLowerEndPt,
                    width: WallConstants.ColumnSteelGrooveLowerWidth,
                    depth: WallConstants.ColumnSteelGrooveDepth,
                    grooveType: GrooveType.SteelColumn);

                    momWallData.Features.Add(rightColumnGrooveLower);

                    // 上段：高度 1000mm → 墙顶，槽宽 178，槽深 103
                    var rightUpperGrooveCenterX = momWallData.Length - WallConstants.ColumnSteelGrooveUpperWidth / 2f;
                    var rightUpperStartPt = new Vec2(rightUpperGrooveCenterX, WallConstants.ColumnSteelGrooveBaseOffset);
                    var rightUpperEndPt = new Vec2(rightUpperGrooveCenterX, momWallData.Width);
                    var rightColumnGrooveUpper = new Groove(
                    id: $"SteelCol-{i:D1}-{col.Pn ?? "noPn"}-Upper",
                    side: MachineSide.Top,
                    startPt: rightUpperStartPt,
                    endPt: rightUpperEndPt,
                    width: WallConstants.ColumnSteelGrooveUpperWidth,
                    depth: WallConstants.ColumnSteelGrooveDepth,
                    grooveType: GrooveType.SteelColumn);

                    momWallData.Features.Add(rightColumnGrooveUpper);

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

                // ──生成钢柱侧板槽（按柱子 X 坐标定位左/右中心点）──────
                if (ColumnSide != PropertyConverter.ColumnSide.Middle)
                {
                    AddSidePlateGroovesByColumnX(col.StartPoint.X, momWallData);

                    // ──生成钢柱侧板小槽（Y = 285 单个中心点）──────────
                    AddSidePlateSmallGroovesByColumnX(col.StartPoint.X, momWallData);
                }
            }
        }

        #endregion

        /// <summary>
        /// 将 BimTopPlate DTO 列表转换为 Groove Feature 列表
        /// </summary>
        private static void ConvertTopPlateToFeature(
            List<BimTopPlateDtoV000>? steelFrameColumns, MomWall momWallData)
        {
            // 根据墙厚决定顶板宽度
            float topPlateWidth = GetTopPlateWidth(momWallData.Thickness);

            // ──顶板槽局部坐标
            var topPlateStartPtY = momWallData.Thickness - topPlateWidth / 2;
            var topPlateStartPt = new Vec2(0, topPlateStartPtY);
            var topPlateEndPt = new Vec2(momWallData.Length, topPlateStartPtY);

            // 添加左侧顶板槽
            var topPlateGroove = new Groove(
                    id: $"TopPlate",
                    side: MachineSide.Front,
                    startPt: topPlateStartPt,
                    endPt: topPlateEndPt,
                    width: topPlateWidth,
                    depth: WallConstants.TopPlateGrooveDepth,
                    grooveType: GrooveType.TopPlate);

            momWallData.Features.Add(topPlateGroove);
        }

        /// <summary>
        /// 根据墙厚决定顶板槽宽度
        ///   thickness &lt; 200 → 140
        ///   200 ≤ thickness &lt; 300 → 240
        ///   thickness ≥ 300 → 240（默认）
        /// </summary>
        private static float GetTopPlateWidth(float thickness)
        {
            if (thickness < 200f)
                return 140f;
            return 240f;
        }


        /// <summary>
        /// 将 TopPlate 中 Studs.Points 转换为腰形孔 Hole Feature
        ///   半径 StudSlotHoleRadius（12.5）、总长 StudSlotHoleTotalLength（200）、
        ///   孔深 StudSlotHoleDepth（114）；腰孔沿墙长方向（0° 沿 X 轴）。
        /// </summary>
        private static void ConvertStudsToFeature(
            List<BimTopPlateDtoV000>? topPlates, MomWall momWallData)
        {
            if (topPlates == null || topPlates.Count == 0)
                return;

            for (int i = 0; i < topPlates.Count; i++)
            {
                var topPlate = topPlates[i];
                if (topPlate?.Studs == null || topPlate.Studs.Points == null || topPlate.Studs.Points.Count == 0)
                    continue;

                var studs = topPlate.Studs;
                string pn = string.IsNullOrWhiteSpace(studs.Pn) ? "noPn" : studs.Pn!;

                for (int j = 0; j < studs.Points.Count; j++)
                {
                    var point = studs.Points[j];
                    if (point == null) continue;

                    // ── 特征 ID ───────────────────────────────
                    string id = studs.Points.Count == 1
                        ? $"Studs-{pn}"
                        : $"Studs-{pn}-{j:D2}";

                    // ── 中心点（Front 面：X=墙长，Z=墙高）────
                    var center = new Vec2((float)point.X, WallConstants.StudEdgeDistance);

                    // ── 构造腰形孔 Feature ────────────────────
                    //    总长 200 = 两圆心距(175) + 2 × 半径(12.5)
                    var hole = Hole.CreateSlotted(
                        id: id,
                        side: MachineSide.Front,
                        center: center,
                        radius: WallConstants.StudSlotHoleRadius,
                        depth: WallConstants.StudSlotHoleDepth,
                        slotLength: WallConstants.StudSlotHoleTotalLength
                                    - WallConstants.StudSlotHoleRadius * 2f,
                        throughHole: false);

                    momWallData.Features.Add(hole);
                }
            }
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
                cornerRadius: WallConstants.DevicePocketCornerRadius,
                pocketType: PocketType.SwitchBox);

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

                // ── 3.3 起终点规范化 ──────────────────────────
                if (direction == RebarSlotDirection.Horizontal && startPos.X > endPos.X)
                {
                    (startPos, endPos) = (endPos, startPos);
                    (rod.StartThreading, rod.EndThreading) = (rod.EndThreading, rod.StartThreading);
                }
                else if (direction == RebarSlotDirection.Vertical && startPos.Y > endPos.Y)
                {
                    (startPos, endPos) = (endPos, startPos);
                    (rod.StartThreading, rod.EndThreading) = (rod.EndThreading, rod.StartThreading);
                }

                // ── 3.4 深度按方向选择 ────────────────────────
                float depth = direction == RebarSlotDirection.Horizontal
                    ? rebar.HorizontalDepth
                    : rebar.VerticalDepth;

                // ── 3.5 加工面按 Z 值判定 ─────────────────────
                float avgZ = (rod.StartPoint.Y + rod.EndPoint.Y) * 0.5f;
                MachineSide side = avgZ >= faceZThreshold
                    ? MachineSide.Top
                    : MachineSide.Bottom;

                // ── 3.6 特征 ID ───────────────────────────────
                string pn = string.IsNullOrWhiteSpace(rebar.Pn) ? "noPn" : rebar.Pn!;
                string id = rebar.Rods.Count == 1
                    ? $"Rebar-{pn}"
                    : $"Rebar-{pn}-{i:D2}";

                // ── 3.7 构造 RebarSlot Feature ────────────────
                var rebarSlot = new RebarSlot(
                    id: id,
                    side: side,
                    startPos: startPos,
                    endPos: endPos,
                    diameter: WallConstants.RebarSlotWidth,
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

        #region Propping 斜撑

        /// <summary>
        /// 以斜撑中心 X 为定位，生成斜撑 Pocket、顶部螺栓孔与正面螺栓孔并添加到 MomWall
        /// （斜撑槽 Propping Feature 的生成逻辑暂时注释，需要时取消注释即可）
        /// </summary>
        private static void ConvertProppingToFeature(float centerX,
            MomWall momWallData)
        {
            // ── 斜撑中心点（Y 取墙顶，供 Pocket / 螺栓孔定位）──
            var centerPoints = new List<Vec2>
            {
                new Vec2(centerX, momWallData.Width),
            };

            // ── 斜撑 Pocket（墙顶面，贴顶板槽槽底）─────────────
            AddProppingPocket(centerPoints, momWallData);

            // ── 斜撑螺栓孔（顶部 2 个 + 正面 2 个）─────────────
            AddProppingTopScrewHoles(centerPoints, momWallData);
            AddProppingFrontScrewHoles(centerPoints, momWallData);

            //ProppingConverter.Convert(
            //    centerX: centerX,
            //    momWallData: momWallData,
            //    id: "Propping-01");
        }

        /// <summary>
        /// 读取各中心点的 X 坐标，在墙体顶端（Top 面）贴顶板槽槽底生成 3 个嵌套 Pocket：
        ///   • Pocket 01：长 106 × 宽 310 × 深 4 ，
        ///       中心 Y = wallWidth - TopPlateGrooveDepth - 310/2
        ///   • Pocket 02：长 106 × 宽 18  × 深 36 ，
        ///       中心 Y = wallWidth - TopPlateGrooveDepth - 18/2
        ///   • Pocket 03：长 252 × 宽 4   × 深 100，
        ///       中心 Y = wallWidth - TopPlateGrooveDepth - 4/2
        /// 仅使用输入点的 X 参与定位，Y 由墙宽（<see cref="MomWall.Width"/>）推算。
        /// 槽长沿墙长方向（X），槽宽沿墙高方向（Y），加工面为墙顶面（Top）。
        /// </summary>
        /// <param name="centerPoints">斜撑中心点列表（仅使用 X 值）</param>
        /// <param name="momWallData">目标墙体</param>
        public static void AddProppingPocket(
            List<Vec2> centerPoints, MomWall momWallData)
        {
            if (centerPoints == null || centerPoints.Count == 0) return;
            if (momWallData == null) return;

            // ── 顶板槽槽底 Y（所有 Pocket 的定位基准）────────────
            float grooveBottomY = momWallData.Width - WallConstants.TopPlateGrooveDepth;

            for (int i = 0; i < centerPoints.Count; i++)
            {
                float centerX = centerPoints[i].X;
                string key = $"{centerX:F0}-{i:D2}";

                // ── Pocket 01：长 106 × 宽 310 × 深 4 ────────────
                momWallData.Features.Add(new Pocket(
                    id: $"ProppingPocket-{key}-01",
                    side: MachineSide.Top,
                    center: new Vec2(centerX,
                        grooveBottomY - WallConstants.TopPlatePocket0Width / 2f),
                    length: WallConstants.TopPlatePocket0Length,
                    width: WallConstants.TopPlatePocket0Width,
                    depth: WallConstants.TopPlatePocket0Depth,
                    pocketType: PocketType.Propping));

                // ── Pocket 02：长 106 × 宽 18 × 深 36 ────────────
                momWallData.Features.Add(new Pocket(
                    id: $"ProppingPocket-{key}-02",
                    side: MachineSide.Top,
                    center: new Vec2(centerX,
                        grooveBottomY - WallConstants.TopPlatePocket1Width / 2f),
                    length: WallConstants.TopPlatePocket1Length,
                    width: WallConstants.TopPlatePocket1Width,
                    depth: WallConstants.TopPlatePocket1Depth,
                    pocketType: PocketType.Propping));

                // ── Pocket 03：长 252 × 宽 4 × 深 100 ────────────
                momWallData.Features.Add(new Pocket(
                    id: $"ProppingPocket-{key}-03",
                    side: MachineSide.Top,
                    center: new Vec2(centerX,
                        grooveBottomY - WallConstants.TopPlatePocket2Width / 2f),
                    length: WallConstants.TopPlatePocket2Length,
                    width: WallConstants.TopPlatePocket2Width,
                    depth: WallConstants.TopPlatePocket2Depth,
                    pocketType: PocketType.Propping));
            }
        }
        #endregion

        #region X 形槽
        /// <summary>
        /// 当墙类型为 CrossBraceWall 时，生成 X 形斜撑钢槽
        /// 
        /// 端点规则（基于钢柱中心线与墙顶/底边的交点）：
        ///   • 左上：左侧钢柱中心 X，墙顶 Y + XBraceTopOffset（向外）
        ///   • 左下：左侧钢柱中心 X，墙底 Y - XBraceBottomOffset（向外）
        ///   • 右上：右侧钢柱中心 X，墙顶 Y + XBraceTopOffset
        ///   • 右下：右侧钢柱中心 X，墙底 Y - XBraceBottomOffset
        /// 
        /// 生成规则：
        ///   ① 求两条交线（左下→右上、右下→左上）的交点
        ///   ② 自交点沿交线方向向下偏移 XBraceCenterOffset（450mm）作为分割点，
        ///      将每条交线分为 2 段：下段（底端 → 分割点）槽宽 80，
        ///      上段（分割点 → 顶端）槽宽 106，槽深均为 90
        ///   ③ 交点处再调用 AddXBraceCenterGrooves 沿 4 条交线方向各生成 1 个槽
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


            // ── 5. 计算 4 个端点 ────────────────────────────────
            Vec2 leftTop = new Vec2(leftColumnX, wallTopY + WallConstants.XBraceTopOffset);
            Vec2 leftBottom = new Vec2(leftColumnX, wallBottomY - WallConstants.XBraceBottomOffset);
            Vec2 rightTop = new Vec2(rightColumnX, wallTopY + WallConstants.XBraceTopOffset);
            Vec2 rightBottom = new Vec2(rightColumnX, wallBottomY - WallConstants.XBraceBottomOffset);


            string pnPrefix = string.IsNullOrWhiteSpace(dto.Pn) ? "Wall" : dto.Pn;

            // ── 6. 求两条交线的交点 ─────────────────────────────
            if (!TryIntersect(leftBottom, rightTop, leftTop, rightBottom,
                              out Vec2 center))
            {
                Console.WriteLine(
                    $"[WARN] CrossBraceWall (Pn={dto.Pn ?? "noPn"}) " +
                    $"两条交线平行，无法求交点，已跳过");
                return;
            }

            // ── 7. 两条交线（底端 → 顶端）───────────────────────
            var lines = new (Vec2 Bottom, Vec2 Top)[]
            {
                (leftBottom, rightTop),   // ① 左下 → 右上
                (rightBottom, leftTop),   // ② 右下 → 左上
            };

            // ── 8. 每条交线自分割点分为下 / 上两段 ──────────────
            //    分割点 = 交点沿交线方向向下偏移 XBraceCenterOffset
            //    下段（底端 → 分割点）槽宽 80，上段（分割点 → 顶端）槽宽 106，
            //    槽深均为 90
            for (int k = 0; k < lines.Length; k++)
            {
                var (bottom, top) = lines[k];

                float downLength = (bottom - center).Length();
                if (downLength <= WallConstants.XBraceCenterOffset)
                {
                    Console.WriteLine(
                        $"[WARN] XBrace 交线[{k}] 交点向下长度 {downLength:F1}mm 不足分割偏移 " +
                        $"{WallConstants.XBraceCenterOffset}mm，已跳过");
                    continue;
                }

                Vec2 splitPt = center
                             + (bottom - center).Normalize()
                             * WallConstants.XBraceCenterOffset;

                // 下段槽（自底端至分割点）
                momWallData.Features.Add(new Groove(
                    id: $"XBrace-{pnPrefix}-{2 * k + 1:D2}",
                    side: MachineSide.Top,
                    startPt: bottom,
                    endPt: splitPt,
                    width: WallConstants.XBraceGrooveWidth,
                    depth: WallConstants.XBraceGrooveDepth,
                    grooveType: GrooveType.XBraceSteel));

                // 上段槽（自分割点至顶端）
                momWallData.Features.Add(new Groove(
                    id: $"XBrace-{pnPrefix}-{2 * k + 2:D2}",
                    side: MachineSide.Top,
                    startPt: splitPt,
                    endPt: top,
                    width: WallConstants.XBraceUpperGrooveWidth,
                    depth: WallConstants.XBraceGrooveDepth,
                    grooveType: GrooveType.XBraceSteel));
            }

            // ── 9. 交点处：沿 4 条交线方向各生成 1 个槽 ──────────
            Vec2[] armEnds = { leftBottom, rightBottom, leftTop, rightTop };
            AddXBraceCenterGrooves(center, armEnds, pnPrefix, momWallData);
        }

        /// <summary>
        /// 在交点对应的 4 条交线方向上生成 4 个槽：
        /// 槽沿交线方向，长 XBraceCenterGrooveLength（100mm）、
        /// 宽 XBraceCenterGrooveWidth（150mm）、深 XBraceCenterGrooveDepth（120mm），
        /// 槽中心点距交点 XBraceCenterGrooveOffset（500mm）。
        /// </summary>
        private static void AddXBraceCenterGrooves(
            Vec2 center,
            Vec2[] armEnds,
            string pnPrefix,
            MomWall momWallData)
        {
            for (int k = 0; k < armEnds.Length; k++)
            {
                Vec2 dir = (armEnds[k] - center).Normalize();

                // 槽中心距交点 500mm，槽长 100mm ⇒ 起终点距交点 450 / 550mm
                Vec2 slotCenter = center + dir * WallConstants.XBraceCenterGrooveOffset;
                Vec2 slotStart = slotCenter - dir * (WallConstants.XBraceCenterGrooveLength / 2f);
                Vec2 slotEnd = slotCenter + dir * (WallConstants.XBraceCenterGrooveLength / 2f);

                momWallData.Features.Add(new Groove(
                    id: $"XBraceCenter-{pnPrefix}-{k + 1:D2}",
                    side: MachineSide.Top,
                    startPt: slotStart,
                    endPt: slotEnd,
                    width: WallConstants.XBraceCenterGrooveWidth,
                    depth: WallConstants.XBraceCenterGrooveDepth,
                    grooveType: GrooveType.XBraceSteel));
            }
        }

        #endregion

        #region 侧板小槽
        /// <summary>
        /// 根据柱子 X 坐标确定钢柱侧板小槽中心点位置，并生成侧板小槽
        /// 
        /// 中心点 X：
        ///   • 左侧柱（X &lt; SidePlateColumnLeftThreshold）：
        ///     X0 = 柱 X + SidePlateSmallGrooveLength/2 + ColumnSteelGrooveWidth/2
        ///                + ColumnSteelWidthClearancePerSide
        ///   • 右侧柱：
        ///     X0 = 柱 X - SidePlateSmallGrooveLength/2 - ColumnSteelGrooveWidth/2
        ///                - ColumnSteelWidthClearancePerSide
        /// 
        /// 中心点 Y：SidePlateSmallGrooveCenterY（285）
        /// 
        /// 生成中心点后调用 <see cref="AddSteelColumnSidePlateSmallGroovesAndScrewHoles"/> 生成侧板小槽。
        /// </summary>
        private static void AddSidePlateSmallGroovesByColumnX(
            float columnX, MomWall momWallData)
        {
            // ── 1. 左 / 右柱判定（X < 阈值 → 左侧柱，其余按右侧柱）──
            bool isLeft = columnX < WallConstants.SidePlateColumnLeftThreshold;

            // ── 2. 中心点 ───────────────────────────────────────
            float offsetX = WallConstants.SidePlateSmallGrooveLength / 2f
                          + WallConstants.ColumnSteelGrooveWidth;

            var center = new Vec2(
                isLeft ? columnX + WallConstants.ColumnSteelGrooveWidth / 2 + offsetX : columnX - WallConstants.ColumnSteelGrooveWidth / 2 - offsetX,
                WallConstants.SidePlateSmallGrooveCenterY);

            // ── 3. 生成侧板小槽 ─────────────────────────────────
            AddSteelColumnSidePlateSmallGroovesAndScrewHoles(center, isLeft, momWallData);
        }

        /// <summary>
        /// 生成钢柱侧板小槽（Pocket 方式加工）：以给定中心点 1 为中心
        ///   • 中心点 1：长 SidePlateSmallGrooveLength（160）× 宽 120 × 深 40
        ///   • 中心点 2：X 由中心点 1 偏移「两槽长度差 / 2」=
        ///       (160 - 15) / 2 = 72.5 —— 左侧钢柱往左偏移、右侧钢柱往右偏移
        ///       在中心点 2 生成窄深槽：长 SidePlateNarrowGrooveLength（15）× 宽 120 × 深 55
        /// 两槽长度沿墙长方向（X），宽度沿墙高方向（Y）。
        /// 生成侧板小槽后一并生成对应的 4 孔螺栓孔（⌀20 × 深 100，以中心点 1 为中心）。
        /// </summary>
        private static void AddSteelColumnSidePlateSmallGroovesAndScrewHoles(
            Vec2 center1, bool isLeftColumn, MomWall momWallData)
        {
            string key = $"{center1.X:F0}-{center1.Y:F0}";

            // ── 中心点 1：长 160 × 宽 120 × 深 40 ───────────────
            momWallData.Features.Add(new Pocket(
                id: $"SidePlateSmall-{key}-01",
                side: MachineSide.Top,
                center: center1,
                length: WallConstants.SidePlateSmallGrooveLength,
                width: WallConstants.SidePlateSmallGrooveWidth,
                depth: WallConstants.SidePlateSmallGrooveDepth,
                pocketType: PocketType.SteelColumnSidePocket));

            // ── 中心点 2：X 偏移两槽长度差 / 2（左柱向左、右柱向右）──
            float offsetX = (WallConstants.SidePlateSmallGrooveLength
                           - WallConstants.SidePlateNarrowGrooveLength) / 2f;

            var center2 = new Vec2(
                isLeftColumn ? center1.X - offsetX : center1.X + offsetX,
                center1.Y);

            // ── 中心点 2：长 15 × 宽 120 × 深 55 ────────────────
            momWallData.Features.Add(new Pocket(
                id: $"SidePlateSmall-{key}-02",
                side: MachineSide.Top,
                center: center2,
                length: WallConstants.SidePlateNarrowGrooveLength,
                width: WallConstants.SidePlateNarrowGrooveWidth,
                depth: WallConstants.SidePlateNarrowGrooveDepth,
                pocketType: PocketType.SteelColumnSidePocket));

            // ── 对应的 4 孔螺栓孔（以中心点 1 为中心）────────────
            AddSteelColumnSidePlateScrewHoles(
                GenerateSidePlateBoltHoleSquarePositions(center1), momWallData);
        }


        #endregion


        #region 侧板槽
        /// <summary>
        /// 根据柱子 X 坐标确定钢柱侧板槽中心点位置，并在每个中心点生成侧板槽
        /// 
        /// 中心点 X：
        ///   • 左侧柱（X &lt; SidePlateColumnLeftThreshold）：
        ///     X0 = 柱 X + SidePlateGrooveLength/2 + ColumnSteelGrooveWidth/2
        ///                + ColumnSteelWidthClearancePerSide
        ///   • 右侧柱：
        ///     X0 = 柱 X - SidePlateGrooveLength/2 - ColumnSteelGrooveWidth/2
        ///                - ColumnSteelWidthClearancePerSide
        /// 
        /// 中心点 Y（两个）：
        ///   • 下：ColumnSteelGrooveBaseOffset + SidePlateGrooveLowerOffset（1000 + 294）
        ///   • 上：Wall.Width - SidePlateGrooveUpperOffset（墙宽 - 604）
        /// 
        /// 每个中心点调用 <see cref="AddSteelColumnSidePlateGroovesAndScrewHoles"/> 生成侧板槽。
        /// </summary>
        private static void AddSidePlateGroovesByColumnX(
            float columnX, MomWall momWallData)
        {
            // ── 1. 左 / 右柱判定（X < 阈值 → 左侧柱，其余按右侧柱）──
            bool isLeft = columnX < WallConstants.SidePlateColumnLeftThreshold;

            // ── 2. 中心点 X ─────────────────────────────────────
            float offsetX = WallConstants.SidePlateGrooveLength / 2f
                          + WallConstants.ColumnSteelGrooveUpperWidth;

            float centerX = isLeft ? columnX + WallConstants.ColumnSteelGrooveWidth / 2 + offsetX : columnX - WallConstants.ColumnSteelGrooveWidth / 2 - offsetX;

            // ── 3. 中心点 Y（下 / 上两个位置）───────────────────
            float[] centerYs =
            {
                WallConstants.ColumnSteelGrooveBaseOffset
                    + WallConstants.SidePlateGrooveLowerOffset,
                momWallData.Width - WallConstants.SidePlateGrooveUpperOffset,
            };

            // ── 4. 生成侧板槽 ───────────────────────────────────
            foreach (float centerY in centerYs)
            {
                AddSteelColumnSidePlateGroovesAndScrewHoles(
                    new Vec2(centerX, centerY), momWallData);
            }
        }

        /// <summary>
        /// 生成钢柱侧板槽（Pocket 方式加工）：以给定中心点为中心，同一中心点开 2 个槽
        ///   • 外槽：长 SidePlateGrooveLength（180）× 宽 SidePlateGrooveOuterWidth（200）× 深 15
        ///   • 内槽：长 180 × 宽 SidePlateGrooveInnerWidth（120）× 深 40
        /// 槽长沿墙长方向（X），槽宽沿墙高方向（Y），两槽同心；
        /// 均由 BoxHandler 按 Pocket 生成 T8 铣槽指令。
        /// 生成侧板槽后一并生成对应的 6 孔螺栓孔（⌀20 × 深 100）。
        /// </summary>
        private static void AddSteelColumnSidePlateGroovesAndScrewHoles(
            Vec2 center, MomWall momWallData)
        {
            string key = $"{center.X:F0}-{center.Y:F0}";

            // 外槽（长 180，宽 200，深 15）
            momWallData.Features.Add(new Pocket(
                id: $"SidePlate-{key}-01",
                side: MachineSide.Top,
                center: center,
                length: WallConstants.SidePlateGrooveLength,
                width: WallConstants.SidePlateGrooveOuterWidth,
                depth: WallConstants.SidePlateGrooveOuterDepth,
                pocketType: PocketType.SteelColumnSidePocket));

            // 内槽（长 180，宽 120，深 40）
            momWallData.Features.Add(new Pocket(
                id: $"SidePlate-{key}-02",
                side: MachineSide.Top,
                center: center,
                length: WallConstants.SidePlateGrooveLength,
                width: WallConstants.SidePlateGrooveInnerWidth,
                depth: WallConstants.SidePlateGrooveInnerDepth,
                pocketType: PocketType.SteelColumnSidePocket));

            // ── 对应的 6 孔螺栓孔 ───────────────────────────────
            AddSteelColumnSidePlateScrewHoles(
                GenerateSidePlateBoltHolePositions(center), momWallData);
        }

        #endregion


        #region 螺栓孔
        /// <summary>
        /// 依据中心点坐标（X0, Y0）生成 6 个螺栓孔位置坐标：
        ///   (X0-60, Y0-80)、(X0-60, Y0+80)、(X0, Y0-80)、
        ///   (X0+60, Y0+80)、(X0+60, Y0-80)、(X0, Y0+80)
        /// 偏移量取自 WallConstants.SidePlateBoltHoleOffsetX / Y。
        /// </summary>
        private static List<Vec2> GenerateSidePlateBoltHolePositions(Vec2 center)
        {
            float dx = WallConstants.SidePlateBoltHoleOffsetX;   // 60
            float dy = WallConstants.SidePlateBoltHoleOffsetY;   // 80

            return new List<Vec2>
            {
                new Vec2(center.X - dx, center.Y - dy),
                new Vec2(center.X - dx, center.Y + dy),
                new Vec2(center.X,      center.Y - dy),
                new Vec2(center.X,      center.Y + dy),
                new Vec2(center.X + dx, center.Y - dy),
                new Vec2(center.X + dx, center.Y + dy),
            };
        }

        /// <summary>
        /// 依据中心点坐标（X0, Y0）生成 4 个螺栓孔位置坐标（方阵）：
        ///   (X0-30, Y0-30)、(X0-30, Y0+30)、(X0+30, Y0+30)、(X0+30, Y0-30)
        /// 偏移量取自 WallConstants.SidePlateBoltHoleSquareOffset。
        /// </summary>
        private static List<Vec2> GenerateSidePlateBoltHoleSquarePositions(Vec2 center)
        {
            float d = WallConstants.SidePlateBoltHoleSquareOffset;   // 30

            return new List<Vec2>
            {
                new Vec2(center.X - d, center.Y - d),
                new Vec2(center.X - d, center.Y + d),
                new Vec2(center.X + d, center.Y + d),
                new Vec2(center.X + d, center.Y - d),
            };
        }

        /// <summary>
        /// 依据中心点坐标（X0, Y0）生成 2 个斜撑顶部螺栓孔位置坐标：
        ///   (X0-30, wallWidth-299)、(X0+30, wallWidth-299)
        /// 其中 wallWidth 取 center.Y，调用方需传入墙顶中心点
        /// （即 Y = <see cref="MomWall.Width"/>）。
        /// 偏移量取自 WallConstants.ProppingTopBoltHoleOffsetX / OffsetY。
        /// </summary>
        private static List<Vec2> GenerateProppingTopBoltHolePositions(Vec2 center)
        {
            float dx = WallConstants.ProppingTopBoltHoleOffsetX;   // 30
            float y = center.Y - WallConstants.ProppingTopBoltHoleOffsetY;   // wallWidth - 299

            return new List<Vec2>
            {
                new Vec2(center.X - dx, y),
                new Vec2(center.X + dx, y),
            };
        }

        /// <summary>
        /// 依据中心点坐标（X0, Y0）生成 2 个斜撑正面螺栓孔位置坐标：
        ///   (X0-70, wallHeight-75)、(X0+70, wallHeight-75)
        /// 其中 wallHeight 取 center.Y，调用方需传入墙顶中心点
        /// （即 Y = <see cref="MomWall.Width"/>）。
        /// 偏移量取自 WallConstants.ProppingFrontBoltHoleOffsetX / OffsetY。
        /// </summary>
        private static List<Vec2> GenerateProppingFrontBoltHolePositions(Vec2 center, MomWall momWallData)
        {
            float dx = WallConstants.ProppingFrontBoltHoleOffsetX;   // 70
            float y = momWallData.ActualThickness - WallConstants.ProppingFrontBoltHoleOffsetY;   // wallHeight - 75

            return new List<Vec2>
            {
                new Vec2(center.X - dx, y),
                new Vec2(center.X + dx, y),
            };
        }

        /// <summary>
        /// 依据斜撑中心点列表生成顶部螺栓孔（圆孔）：
        ///   孔径 WallConstants.ProppingScrewHoleDiameter（20mm），
        ///   孔深 WallConstants.ProppingScrewHoleDepth（100mm），
        ///   加工面为墙顶面（Top），非通孔。
        /// 每个中心点生成 2 个孔，位置由 <see cref="GenerateProppingTopBoltHolePositions"/> 计算。
        /// </summary>
        public static void AddProppingTopScrewHoles(
            List<Vec2> centerPoints, MomWall momWallData)
        {
            if (centerPoints == null || centerPoints.Count == 0) return;
            if (momWallData == null) return;

            foreach (var center in centerPoints)
            {
                AddProppingScrewHoles(
                    GenerateProppingTopBoltHolePositions(center),
                    momWallData,
                    MachineSide.Top,
                    idPrefix: "ProppingTopScrew");
            }
        }

        /// <summary>
        /// 依据斜撑中心点列表生成正面螺栓孔（圆孔）：
        ///   孔径 WallConstants.ProppingScrewHoleDiameter（20mm），
        ///   孔深 WallConstants.ProppingScrewHoleDepth（100mm），
        ///   加工面为墙正面（Front），非通孔。
        /// 每个中心点生成 2 个孔，位置由 <see cref="GenerateProppingFrontBoltHolePositions"/> 计算。
        /// </summary>
        public static void AddProppingFrontScrewHoles(
            List<Vec2> centerPoints, MomWall momWallData)
        {
            if (centerPoints == null || centerPoints.Count == 0) return;
            if (momWallData == null) return;

            foreach (var center in centerPoints)
            {
                AddProppingScrewHoles(
                    GenerateProppingFrontBoltHolePositions(center, momWallData),
                    momWallData,
                    MachineSide.Front,
                    idPrefix: "ProppingFrontScrew");
            }
        }

        /// <summary>
        /// 依据孔位坐标列表批量生成斜撑螺栓孔（圆孔），孔径 20、孔深 100、非通孔。
        /// 特征 ID 由「前缀 + 孔中心坐标」生成，多个中心点调用不会重名。
        /// </summary>
        private static void AddProppingScrewHoles(
            List<Vec2> positions, MomWall momWallData,
            MachineSide side, string idPrefix)
        {
            if (positions == null || positions.Count == 0) return;

            foreach (var pos in positions)
            {
                momWallData.Features.Add(Hole.CreateRound(
                    id: $"{idPrefix}-{pos.X:F0}-{pos.Y:F0}",
                    side: side,
                    center: pos,
                    radius: WallConstants.ProppingScrewHoleDiameter / 2f,
                    depth: WallConstants.ProppingScrewHoleDepth,
                    throughHole: false));
            }
        }

        /// <summary>
        /// 依据螺栓孔中心点坐标列表生成钢柱侧板螺栓孔（圆孔）：
        ///   孔径 WallConstants.SidePlateScrewHoleDiameter（20mm），
        ///   孔深 WallConstants.SidePlateScrewHoleDepth（100mm），
        ///   加工面为墙正面（Top），非通孔。
        /// 特征 ID 由孔中心坐标生成，多个柱子/多个中心点调用不会重名。
        /// </summary>
        private static void AddSteelColumnSidePlateScrewHoles(
            List<Vec2> centerPoints, MomWall momWallData)
        {
            if (centerPoints == null || centerPoints.Count == 0) return;

            foreach (var center in centerPoints)
            {
                momWallData.Features.Add(Hole.CreateRound(
                    id: $"SidePlateScrew-{center.X:F0}-{center.Y:F0}",
                    side: MachineSide.Top,
                    center: center,
                    radius: WallConstants.SidePlateScrewHoleDiameter / 2f,
                    depth: WallConstants.SidePlateScrewHoleDepth,
                    throughHole: false));
            }
        }
        #endregion


        /// <summary>
        /// 求两条直线（各由两点确定）的交点；两线平行（含重合）时返回 false
        /// </summary>
        private static bool TryIntersect(
            Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2, out Vec2 intersection)
        {
            intersection = Vec2.Zero;

            Vec2 da = a2 - a1;
            Vec2 db = b2 - b1;

            float denom = da.Cross(db);
            if (MathF.Abs(denom) <= WallConstants.DirectionTolerance)
                return false;

            float t = (b1 - a1).Cross(db) / denom;
            intersection = a1 + da * t;
            return true;
        }

    }
}
