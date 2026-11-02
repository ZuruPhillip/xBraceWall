using BimWallData.V002;
using CncWallStation.Consts;
using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.Features.MepSlots;
using CncWallStation.MomWallData;
using CncWallStation.Transforms;
using Infrastructure.Maths;
using Newtonsoft.Json;

namespace CncWallStation.VersionMappers
{
    public class BimWallMapperV002 : IBimWallMapper
    {
        public string SupportedVersion => "0.0.2";

        public MomWall Map(string json)
        {
            var dto = JsonConvert.DeserializeObject<BimWallDtoV002>(json);

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            dto.Validate();
            ValidateV002(dto);

            MomWall momWallData = new MomWall(dto.Id, WallElevationConverter.ToVec2Outline(dto.AacWallElevation.Contour), dto.CoreThickness);

            // 生成钢柱槽数据（V002 使用 columnAssemblies）
            ConvertColumnAssembliesToFeatures(dto.ColumnAssemblies, momWallData);

            // 生成顶板槽数据
            ConvertTopPlateToFeature(dto.TopPlate, momWallData);

            // 生成剪力钉孔数据
            ConvertStudsToFeature(dto.TopPlate, momWallData);

            // 生成 X 形斜撑数据（wallType 为 CrossBraceWall 时）
            ConvertCrossBraceToFeature(
                dto.WallType, CollectColumnXs(dto.ColumnAssemblies), dto.Pn, momWallData);

            // 生成胶水密封槽数据
            GenerateGlueSealFeature(momWallData);

            // 生成钢筋槽数据
            ConvertRebarSlotToFeature(dto.Rebars, momWallData);

            // 生成 BendingKey 数据（V002 使用 shearKeys）
            ConvertShearKeysToFeature(dto.ShearKeys, momWallData);

            // 生成 MepCableSlot 数据（V002 加工面/类型从 mepCableCutouts 关联）
            ConvertMepCableToFeature(dto.MepCables, dto.MepCableCutouts, momWallData);

            // 生成 MepCableCutout 线槽数据（V002 新增）
            ConvertMepCableCutoutsToFeature(dto.MepCableCutouts, momWallData);

            // 生成设备盒线槽数据
            ConvertDeviceToFeature(dto.MepDevices, momWallData);

            // 生成斜撑数据（与 V000 一致：固定中心 X 定位，忽略 proppingConnectors 数据）
            ConvertProppingToFeature(2000f, momWallData);

            // 生成窗户数据
            ConvertOpeningToFeature(dto.OpeningHoles, momWallData);

            return momWallData;
        }

        /// <summary>
        /// V002 特有校验：对新增结构做完整性检查，非法项告警并跳过（不抛异常）。
        /// </summary>
        private static void ValidateV002(BimWallDtoV002 dto)
        {
            // mepCableCutouts 类型合法性校验（waffleBox/device 只能是端点）
            if (dto.MepCableCutouts != null)
            {
                foreach (var cutout in dto.MepCableCutouts)
                {
                    if (cutout?.Points == null) continue;
                    for (int i = 1; i < cutout.Points.Count - 1; i++)
                    {
                        string? t = cutout.Points[i].Type?.ToLowerInvariant();
                        if (t == "wafflebox" || t == "device")
                        {
                            Console.WriteLine(
                                $"[WARN] MepCableCutout (sn={cutout.Sn}) 的 {t} 点(index={i})不是端点，已跳过该 cutout");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 提取所有钢柱的中心 X 坐标（V002 使用 columnAssemblies.origin.x）
        /// </summary>
        private static List<float> CollectColumnXs(
            List<BimColumnAssemblyDtoV002>? columnAssemblies)
        {
            var xs = new List<float>();

            if (columnAssemblies == null) return xs;

            foreach (var col in columnAssemblies)
            {
                if (col?.Origin == null) continue;
                xs.Add((float)col.Origin.X);
            }

            return xs;
        }

        #region 钢柱槽
        /// <summary>
        /// 将 ColumnAssemblies DTO 列表转换为 Groove / Pocket Feature 列表
        /// 与 V000 的钢柱转换关系保持一致：
        ///   • 左/右柱：下段（墙底 → 1000mm，宽 103）+ 上段（1000mm → 墙顶，宽 178）钢柱槽
        ///   • 底板槽（BasePlate）
        ///   • 非中间柱：侧板槽（SidePlate，上下两个中心点）、侧板小槽（SidePlateSmall）
        ///     及对应螺栓孔
        /// </summary>
        private static void ConvertColumnAssembliesToFeatures(
            List<BimColumnAssemblyDtoV002>? columnAssemblies, MomWall momWallData)
        {
            if (columnAssemblies == null || columnAssemblies.Count == 0)
                return;

            for (int i = 0; i < columnAssemblies.Count; i++)
            {
                var col = columnAssemblies[i];

                if (col?.Origin == null)
                {
                    Console.WriteLine(
                        $"[WARN] ColumnAssembly[{i}] 缺少 Origin，已跳过");
                    continue;
                }

                float columnX = (float)col.Origin.X;
                var columnSide = PropertyConverter.DetermineColumnSide(columnX, momWallData);

                switch (columnSide)
                {
                    case PropertyConverter.ColumnSide.Left:
                        // ──左侧钢柱槽：贴墙左边，分下/上两段
                        // 下段：墙底 → 高度 1000mm，槽宽 103
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

                        // 上段：高度 1000mm → 墙顶，槽宽 178
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
                if (columnSide != PropertyConverter.ColumnSide.Middle)
                {
                    AddSidePlateGroovesByColumnX(columnX, momWallData);

                    // ──生成钢柱侧板小槽（Y = 285 单个中心点）──────────
                    AddSidePlateSmallGroovesByColumnX(columnX, momWallData);
                }
            }
        }
        #endregion

        /// <summary>
        /// 将 BimTopPlate DTO 列表转换为 Groove Feature 列表
        /// </summary>
        private static void ConvertTopPlateToFeature(
            List<BimTopPlateDtoV002>? topPlates, MomWall momWallData)
        {
            // 根据墙厚决定顶板宽度
            float topPlateWidth = GetTopPlateWidth(momWallData.Thickness);

            // ──顶板槽局部坐标
            var topPlateStartPtY = momWallData.Thickness - topPlateWidth / 2;
            var topPlateStartPt = new Vec2(0, topPlateStartPtY);
            var topPlateEndPt = new Vec2(momWallData.Length, topPlateStartPtY);

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
        /// </summary>
        private static float GetTopPlateWidth(float thickness)
        {
            if (thickness < 200f)
                return 140f;
            return 240f;
        }

        /// <summary>
        /// 将 TopPlate 中 Studs.Points 转换为腰形孔 Hole Feature
        ///   半径 StudSlotHoleRadius（12.5）、总长 StudSlotHoleTotalLength（225）、
        ///   孔深 StudSlotHoleDepth（114）；腰孔沿墙长方向（0° 沿 X 轴）。
        /// </summary>
        private static void ConvertStudsToFeature(
            List<BimTopPlateDtoV002>? topPlates, MomWall momWallData)
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

                    string id = studs.Points.Count == 1
                        ? $"Studs-{pn}"
                        : $"Studs-{pn}-{j:D2}";

                    var center = new Vec2((float)point.X, WallConstants.StudEdgeDistance);

                    // ── 构造腰形孔 Feature ────────────────────
                    //    总长 = 两圆心距 + 2 × 半径
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
            var glueSealStartPtY = WallConstants.GlueSealGrooveWidth / 2;
            var glueSealStartPt = new Vec2(0, glueSealStartPtY);
            var glueSealEndPt = new Vec2(momWallData.Length, glueSealStartPtY);

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
        /// 将 BimShearKeys DTO 转换为 BendingKey 腰孔 Hole Feature 列表
        /// 与 V000 的 bendingKeys 输出保持一致（含特征 ID 前缀）
        /// </summary>
        private static void ConvertShearKeysToFeature(
            BimShearKeysDtoV002? shearKeys, MomWall momWallData)
        {
            if (shearKeys == null) return;
            if (shearKeys.Points == null || shearKeys.Points.Count == 0) return;

            for (int i = 0; i < shearKeys.Points.Count; i++)
            {
                var point = shearKeys.Points[i];
                if (point == null) continue;

                string id = shearKeys.Points.Count == 1
                    ? $"BendingKey-{shearKeys.Pn}"
                    : $"BendingKey-{shearKeys.Pn}-{i:D2}";

                var center = new Vec2((float)point.X, (float)point.Y);

                var slottedHole = Hole.CreateSlotted(
                    id: id,
                    side: MachineSide.Back,
                    center: center,
                    radius: WallConstants.BendingKeyHoleRadius,
                    depth: WallConstants.BendingKeyHoleDepth,
                    slotLength: WallConstants.BendingKeyHoleSlotLength,
                    slotAngleDeg: WallConstants.BendingKeyHoleSlotAngleDeg,
                    throughHole: false);

                momWallData.Features.Add(slottedHole);
            }
        }

        /// <summary>
        /// 将 MepCableCutout DTO 列表批量转换为 MepSlot Feature 并添加到 MomWall
        /// </summary>
        private static void ConvertMepCableCutoutsToFeature(
            List<BimMepCableCutoutDtoV002>? mepCableCutouts,
            MomWall momWallData)
        {
            if (mepCableCutouts == null || mepCableCutouts.Count == 0) return;

            foreach (var cutout in mepCableCutouts)
            {
                MepCableConverter.ConvertV002Cutout(cutout, momWallData);
            }
        }

        /// <summary>
        /// 将多条 MepCable DTO 批量转换为 MepSlot Feature。
        ///
        /// V002 规则：
        ///   • mepCables 的 point 仅含 position(x/y/z) 与 sn，不含 frontFace/type；
        ///   • 加工面与端点类型从 mepCableCutouts 按 sn 关联获取；
        ///   • 若关联不到，默认 Top 面 + 普通直线段处理。
        /// </summary>
        private static void ConvertMepCableToFeature(
            List<BimMepCableDtoV002>? mepCables,
            List<BimMepCableCutoutDtoV002>? mepCableCutouts,
            MomWall momWallData)
        {
            if (mepCables == null || mepCables.Count == 0) return;

            // 构建 sn → cutout point 映射，便于关联 frontFace / type
            var cutoutPointMap = BuildCutoutPointMap(mepCableCutouts);

            foreach (var mepCable in mepCables)
            {
                if (mepCable?.Points == null || mepCable.Points.Count < 2) continue;

                var pts = mepCable.Points
                    .Where(p => p.Position != null)
                    .ToList();

                if (pts.Count < 2) continue;

                // ── 加工面：取首点关联到的 frontFace（缺省 Top）──────────────
                bool isFront = true;
                if (cutoutPointMap.TryGetValue(pts[0].Sn, out var firstCutoutPt))
                    isFront = firstCutoutPt.FrontFace;

                MachineSide side = isFront ? MachineSide.Top : MachineSide.Bottom;

                string id = string.IsNullOrWhiteSpace(mepCable.Pn)
                    ? $"MepCable-{mepCable.Sn}"
                    : $"MepCable-{mepCable.Pn}";

                var slot = momWallData.AddMepSlot(id, side, width: WallConstants.MepCableSlotWidth);
                bool first = true;

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var startPos = new Vec2((float)pts[i].Position.X, (float)pts[i].Position.Z);
                    var endPos = new Vec2((float)pts[i + 1].Position.X, (float)pts[i + 1].Position.Z);

                    if (first)
                    {
                        slot.AddLine(startPos, endPos, WallConstants.MepCableSlotDepth);
                        first = false;
                    }
                    else
                    {
                        slot.LineTo(endPos, WallConstants.MepCableSlotDepth);
                    }
                }
            }
        }

        /// <summary>
        /// 构建 sn → cutout point 映射，用于 mepCable 关联 frontFace / type。
        /// </summary>
        private static Dictionary<int, BimMepCableCutoutPointDtoV002> BuildCutoutPointMap(
            List<BimMepCableCutoutDtoV002>? mepCableCutouts)
        {
            var map = new Dictionary<int, BimMepCableCutoutPointDtoV002>();

            if (mepCableCutouts == null) return map;

            foreach (var cutout in mepCableCutouts)
            {
                if (cutout?.Points == null) continue;
                foreach (var point in cutout.Points)
                {
                    if (point != null && !map.ContainsKey(point.Sn))
                        map[point.Sn] = point;
                }
            }

            return map;
        }

        /// <summary>
        /// 将 Mep Device DTO 转换为 Pocket Feature。
        ///
        /// V002 规则：device 无 frontFace，加工面按 position.z 判定
        ///   • z >= 墙厚/2 → Top
        ///   • z <  墙厚/2 → Bottom
        /// </summary>
        private static void ConvertDeviceToFeature(
            List<BimMepDeviceDtoV002>? devices, MomWall momWallData)
        {
            if (devices == null || devices.Count == 0) return;

            foreach (var device in devices)
            {
                if (device == null) continue;
                if (device.Position == null)
                {
                    Console.WriteLine($"[WARN] MepDevice [{device.Pn}] 缺少 Position，已跳过");
                    continue;
                }

                float faceZThreshold = momWallData.Thickness / 2f;
                MachineSide side = device.Position.Z >= faceZThreshold
                    ? MachineSide.Top
                    : MachineSide.Bottom;

                string id = string.IsNullOrWhiteSpace(device.Pn)
                    ? $"MepDevice-{device.Position.X:F0}-{device.Position.Y:F0}"
                    : $"MepDevice-{device.Pn}";

                var center = new Vec2(
                    (float)device.Position.X,
                    (float)device.Position.Z);

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
        }

        /// <summary>
        /// 将单个 BimRebar DTO 转换为若干 RebarSlot Feature 并加入 MomWall
        /// </summary>
        private static void ConvertRebarSlotToFeature(
            BimRebarDtoV002? rebar, MomWall momWallData)
        {
            if (rebar == null) return;

            if (rebar.Rods == null || rebar.Rods.Count == 0)
            {
                Console.WriteLine(
                    $"[WARN] Rebar (Pn={rebar.Pn ?? "noPn"}) 缺少 Rods，已跳过");
                return;
            }

            float faceZThreshold = momWallData.Thickness / 2f;

            for (int i = 0; i < rebar.Rods.Count; i++)
            {
                var rod = rebar.Rods[i];

                if (rod == null || rod.StartPoint == null || rod.EndPoint == null)
                {
                    Console.WriteLine(
                        $"[WARN] Rebar (Pn={rebar.Pn ?? "noPn"}) Rod[{i}] 起终点缺失，已跳过");
                    continue;
                }

                var startPos = new Vec2(rod.StartPoint.X, rod.StartPoint.Z);
                var endPos = new Vec2(rod.EndPoint.X, rod.EndPoint.Z);

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

                float depth = direction == RebarSlotDirection.Horizontal
                    ? rebar.HorizontalDepth
                    : rebar.VerticalDepth;

                float avgZ = (rod.StartPoint.Y + rod.EndPoint.Y) * 0.5f;
                MachineSide side = avgZ >= faceZThreshold
                    ? MachineSide.Top
                    : MachineSide.Bottom;

                string pn = string.IsNullOrWhiteSpace(rebar.Pn) ? "noPn" : rebar.Pn!;
                string id = rebar.Rods.Count == 1
                    ? $"Rebar-{pn}"
                    : $"Rebar-{pn}-{i:D2}";

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
        /// 将开洞 DTO 列表批量转换为 Window Feature
        /// </summary>
        private static void ConvertOpeningToFeature(
            List<BimOpeningHoleDtoV002>? openings, MomWall momWallData)
        {
            if (openings == null || openings.Count == 0) return;

            foreach (var opening in openings)
            {
                if (opening == null) continue;

                if (opening.Contour == null || opening.Contour.Count < 3)
                {
                    Console.WriteLine(
                        $"[WARN] Opening (Uuid={opening.Uuid ?? "noUuid"}) " +
                        $"轮廓点不足 3 个，无法构成多边形，已跳过");
                    continue;
                }

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
                    continue;
                }

                string id = string.IsNullOrWhiteSpace(opening.Uuid)
                    ? $"Window-{momWallData.Features.Count(f => f.Type == FeatureType.Window):D2}"
                    : $"Window-{opening.Uuid}";

                var window = new Window(
                    id: id,
                    side: MachineSide.Top,
                    contour: contour,
                    depth: momWallData.Thickness);

                momWallData.Features.Add(window);
            }
        }

        #region Propping 斜撑
        /// <summary>
        /// 以斜撑中心 X 为定位，生成斜撑 Pocket、顶部螺栓孔与正面螺栓孔并添加到 MomWall
        /// （与 V000 一致：使用固定中心 X，斜撑槽 Propping Feature 的生成逻辑暂时注释）
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

            // 历史入口（ProppingConverter 生成的八边形双面槽），当前不启用：
            //ProppingConverter.Convert(
            //    centerX: centerX,
            //    momWallData: momWallData,
            //    id: "Propping-01");
        }

        /// <summary>
        /// 读取各中心点的 X 坐标，在墙体顶端（Top 面）贴顶板槽槽底生成 3 个嵌套 Pocket：
        ///   • Pocket 01：长 106 × 宽 310 × 深 4
        ///   • Pocket 02：长 106 × 宽 18  × 深 36
        ///   • Pocket 03：长 252 × 宽 4   × 深 100
        /// 仅使用输入点的 X 参与定位，Y 由墙宽（<see cref="MomWall.Width"/>）推算。
        /// </summary>
        private static void AddProppingPocket(
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
        /// 当墙类型为 CrossBraceWall 时，生成 X 形斜撑钢槽（与 V000 规则一致）
        ///
        /// 端点规则（基于钢柱中心线与墙顶/底边的交点）：
        ///   • 左上：左侧钢柱中心 X，墙顶 Y + XBraceTopOffset（向外）
        ///   • 左下：左侧钢柱中心 X，墙底 Y - XBraceBottomOffset（向外）
        ///   • 右上：右侧钢柱中心 X，墙顶 Y + XBraceTopOffset
        ///   • 右下：右侧钢柱中心 X，墙底 Y - XBraceBottomOffset
        /// </summary>
        /// <param name="wallType">墙类型（dto.wallType）</param>
        /// <param name="columnXs">所有钢柱中心 X 坐标（dto.columnAssemblies[].origin.x）</param>
        /// <param name="pn">墙体 Pn（用于特征 ID 前缀）</param>
        /// <param name="momWallData">目标墙体</param>
        private static void ConvertCrossBraceToFeature(
            string? wallType, List<float> columnXs, string? pn, MomWall momWallData)
        {
            // ── 1. 仅 CrossBraceWall 才生成 ──────────────────────
            if (!string.Equals(wallType, "CrossBraceWall",
                               StringComparison.OrdinalIgnoreCase))
                return;

            // ── 2. 必须有钢柱数据 ────────────────────────────────
            if (columnXs == null || columnXs.Count < 2)
            {
                Console.WriteLine(
                    $"[WARN] CrossBraceWall (Pn={pn ?? "noPn"}) " +
                    $"钢柱数量 < 2，无法定位斜撑端点，已跳过");
                return;
            }

            // ── 3. 找出最左 / 最右钢柱中心 X ─────────────────────
            float leftColumnX = columnXs.Min();
            float rightColumnX = columnXs.Max();

            if (leftColumnX >= rightColumnX)
            {
                Console.WriteLine(
                    $"[WARN] CrossBraceWall (Pn={pn ?? "noPn"}) " +
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

            string pnPrefix = string.IsNullOrWhiteSpace(pn) ? "Wall" : pn;

            // ── 6. 求两条交线的交点 ─────────────────────────────
            if (!TryIntersect(leftBottom, rightTop, leftTop, rightBottom,
                              out Vec2 center))
            {
                Console.WriteLine(
                    $"[WARN] CrossBraceWall (Pn={pn ?? "noPn"}) " +
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
        ///   • 中心点 1：长 160 × 宽 120 × 深 40
        ///   • 中心点 2：X 偏移（160 - 15）/ 2 = 72.5，左柱向左、右柱向右
        ///     生成窄深槽：长 15 × 宽 120 × 深 55
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
        /// 中心点 Y：下 = 1000 + 294；上 = 墙宽 - 604
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
        ///   • 外槽：长 180 × 宽 200 × 深 15
        ///   • 内槽：长 180 × 宽 120 × 深 40
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
        ///   (X0±60, Y0±80)
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
        /// 依据中心点坐标（X0, Y0）生成 4 个螺栓孔位置坐标（方阵 ±30）
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
        /// 依据中心点坐标生成 2 个斜撑正面螺栓孔位置坐标：
        ///   (X0-70, actualThickness-75)、(X0+70, actualThickness-75)
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
        /// 依据斜撑中心点列表生成顶部螺栓孔（圆孔）
        /// </summary>
        private static void AddProppingTopScrewHoles(
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
        /// 依据斜撑中心点列表生成正面螺栓孔（圆孔）
        /// </summary>
        private static void AddProppingFrontScrewHoles(
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
        ///   孔径 20mm、孔深 100mm，加工面为墙顶面（Top），非通孔。
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
