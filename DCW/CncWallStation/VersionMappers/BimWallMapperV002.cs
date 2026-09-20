using BimWallData.V002;
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

            // 生成胶水密封槽数据
            GenerateGlueSealFeature(momWallData);

            // 生成钢筋槽数据
            ConvertRebarSlotToFeature(dto.Rebars, momWallData);

            // 生成 ShearKeys 数据
            ConvertShearKeysToFeature(dto.ShearKeys, momWallData);

            // 生成 MepCableSlot 数据（V002 加工面/类型从 mepCableCutouts 关联）
            ConvertMepCableToFeature(dto.MepCables, dto.MepCableCutouts, momWallData);

            // 生成 MepCableCutout 线槽数据（V002 新增）
            ConvertMepCableCutoutsToFeature(dto.MepCableCutouts, momWallData);

            // 生成设备盒线槽数据
            ConvertDeviceToFeature(dto.MepDevices, momWallData);

            // 生成斜撑数据（V002 使用 proppingConnectors）
            ConvertProppingToFeature(dto.ProppingConnectors, momWallData);

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
        /// 将 ColumnAssemblies DTO 列表转换为 Groove Feature 列表
        /// </summary>
        private static void ConvertColumnAssembliesToFeatures(
            List<BimColumnAssemblyDtoV002>? columnAssemblies, MomWall momWallData)
        {
            if (columnAssemblies == null || columnAssemblies.Count == 0)
                return;

            for (int i = 0; i < columnAssemblies.Count; i++)
            {
                var col = columnAssemblies[i];

                if (col.Origin == null)
                {
                    Console.WriteLine(
                        $"[WARN] ColumnAssembly[{i}] 缺少 Origin，已跳过");
                    continue;
                }

                var columnSide = PropertyConverter.DetermineColumnSide(col.Origin.X, momWallData);

                switch (columnSide)
                {
                    case PropertyConverter.ColumnSide.Left:
                        // ── 添加左侧钢柱槽
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

                        // ── 顶板槽局部坐标
                        var columnGrooveLeftTopStartPtY = momWallData.Width - WallConstants.TopBracketGrooveWidth / 2;
                        var columnGrooveLeftTopStartPt = new Vec2(0, columnGrooveLeftTopStartPtY);
                        var columnGrooveLeftTopEndPt = new Vec2(WallConstants.TopBracketGrooveLength, columnGrooveLeftTopStartPtY);

                        var leftTopPlateGroove = new Groove(
                                id: $"TopPlate-Left-{i:D1}",
                                side: MachineSide.Top,
                                startPt: columnGrooveLeftTopStartPt,
                                endPt: columnGrooveLeftTopEndPt,
                                width: WallConstants.TopBracketGrooveWidth,
                                depth: WallConstants.TopBracketGrooveDepth,
                                grooveType: GrooveType.TopBracket);
                        momWallData.Features.Add(leftTopPlateGroove);

                        // ── 底板槽局部坐标
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
                        // ── 添加右侧钢柱槽
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

                        // ── 顶板槽局部坐标
                        var columnGrooveRightTopStartPtY = momWallData.Width - WallConstants.TopBracketGrooveWidth / 2;
                        var columnGrooveTopRightStartPt = new Vec2(momWallData.Length - WallConstants.TopBracketGrooveLength, columnGrooveRightTopStartPtY);
                        var columnGrooveTopRightEndPt = new Vec2(momWallData.Length, columnGrooveRightTopStartPtY);

                        var rightTopPlateGroove = new Groove(
                                id: $"TopPlate-Right-{i:D1}",
                                side: MachineSide.Top,
                                startPt: columnGrooveTopRightStartPt,
                                endPt: columnGrooveTopRightEndPt,
                                width: WallConstants.TopBracketGrooveWidth,
                                depth: WallConstants.TopBracketGrooveDepth,
                                grooveType: GrooveType.TopBracket);
                        momWallData.Features.Add(rightTopPlateGroove);

                        // ── 底板槽局部坐标
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
            }
        }

        /// <summary>
        /// 将 BimTopPlate DTO 列表转换为 Groove Feature 列表
        /// </summary>
        private static void ConvertTopPlateToFeature(
            List<BimTopPlateDtoV002>? topPlates, MomWall momWallData)
        {
            if (topPlates == null || topPlates.Count == 0)
                return;

            // 根据墙厚决定顶板宽度
            float topPlateWidth = GetTopPlateWidth(momWallData.Thickness);

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
        /// 将 TopPlate 中 Studs.Points 转换为圆孔 Hole Feature
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
                float radius = WallConstants.StudDiameter / 2f;

                for (int j = 0; j < studs.Points.Count; j++)
                {
                    var point = studs.Points[j];
                    if (point == null) continue;

                    string id = studs.Points.Count == 1
                        ? $"Studs-{pn}"
                        : $"Studs-{pn}-{j:D2}";

                    var center = new Vec2((float)point.X, WallConstants.StudEdgeDistance);

                    var hole = Hole.CreateRound(
                        id: id,
                        side: MachineSide.Front,
                        center: center,
                        radius: radius,
                        depth: WallConstants.StudHoleDepth,
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
                    ? $"ShearKey-{shearKeys.Pn}"
                    : $"ShearKey-{shearKeys.Pn}-{i:D2}";

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
                    (float)device.Position.Y);

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

        /// <summary>
        /// 将 ProppingConnectors DTO 转换为 Propping Feature
        /// </summary>
        private static void ConvertProppingToFeature(
            BimProppingConnectorsDtoV002? proppingConnectors,
            MomWall momWallData)
        {
            if (proppingConnectors == null) return;

            int connectorIndex = 0;

            ConvertProppingItemsToFeature(proppingConnectors.ColumnBracket, "ColumnBracket", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.Standard, "Standard", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TopBracket, "TopBracket", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TypeA, "TypeA", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TypeB, "TypeB", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TypeC, "TypeC", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TypeD, "TypeD", momWallData, ref connectorIndex);
        }

        private static void ConvertProppingItemsToFeature(
            List<BimProppingConnectorItemDtoV002>? items,
            string connectorType,
            MomWall momWallData,
            ref int connectorIndex)
        {
            if (items == null || items.Count == 0) return;

            foreach (var item in items)
            {
                if (item?.Position == null) continue;

                float centerX = (float)item.Position.X;
                string id = $"Propping-{connectorType}-{connectorIndex:D2}";

                ProppingConverter.Convert(
                    centerX: centerX,
                    momWallData: momWallData,
                    id: id);

                connectorIndex++;
            }
        }
    }
}
