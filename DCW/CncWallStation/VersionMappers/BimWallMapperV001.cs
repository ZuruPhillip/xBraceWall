using BimWallData.V001;
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
        public string SupportedVersion => "0.0.1";

        public MomWall Map(string json)
        {
            var dto = JsonConvert.DeserializeObject<BimWallDtoV001>(json);

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            dto.Validate();

            MomWall momWallData = new MomWall(dto.Id, WallElevationConverter.ToVec2Outline(dto.AacWallElevation.Contour), dto.CoreThickness);

            // 生成钢柱槽数据（V001 使用 columnAssemblies）
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

            // 生成 MepCableSlot 数据
            ConvertMepCableToFeature(dto.MepCables, momWallData);

            // 生成设备盒线槽数据
            ConvertDeviceToFeature(dto.MepDevices, momWallData);

            // 生成斜撑数据（V001 使用 proppingConnectors）
            ConvertProppingToFeature(dto.ProppingConnectors, momWallData);

            // 生成窗户数据
            ConvertOpeningToFeature(dto.OpeningHoles, momWallData);

            return momWallData;
        }

        /// <summary>
        /// 将 ColumnAssemblies DTO 列表转换为 Groove Feature 列表
        /// V001 新增：使用 columnAssemblies 替代 V000 的 steelFrameColumns
        /// </summary>
        private static void ConvertColumnAssembliesToFeatures(
            List<BimColumnAssemblyDtoV001>? columnAssemblies, MomWall momWallData)
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

                var ColumnSide = PropertyConverter.DetermineColumnSide(col.Origin.X, momWallData);

                switch (ColumnSide)
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
            List<BimTopPlateDtoV001>? topPlates, MomWall momWallData)
        {
            if (topPlates == null || topPlates.Count == 0)
                return;

            var topPlateStartPtY = momWallData.Thickness - WallConstants.TopPlateGrooveWidth / 2;
            var topPlateStartPt = new Vec2(0, topPlateStartPtY);
            var topPlateEndPt = new Vec2(momWallData.Length, topPlateStartPtY);

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
        /// 将 TopPlate 中 Studs.Points 转换为圆孔 Hole Feature
        /// </summary>
        private static void ConvertStudsToFeature(
            List<BimTopPlateDtoV001>? topPlates, MomWall momWallData)
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
        /// V001 新增：shearKeys 即 BendingKey，使用 BendingKey 参数
        /// </summary>
        private static void ConvertShearKeysToFeature(
            BimShearKeysDtoV001? shearKeys, MomWall momWallData)
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
        /// 将多条 MepCable DTO 批量转换为 MepSlot Feature 并添加到 MomWall
        /// </summary>
        private static void ConvertMepCableToFeature(
            List<BimMepCableDtoV001>? mepCables,
            MomWall momWallData)
        {
            if (mepCables == null || mepCables.Count == 0) return;

            foreach (var mepCable in mepCables)
            {
                MepCableConverter.ConvertV001(mepCable, momWallData);
            }
        }

        /// <summary>
        /// 将单个 Mep Device DTO 转换为 Pocket Hole Feature
        /// </summary>
        private static void ConvertDeviceToFeature(
            BimMepDeviceDtoV001? device, MomWall momWallData)
        {
            if (device == null) return;
            if (device.Position == null)
            {
                Console.WriteLine($"[WARN] MepDevice [{device.Pn}] 缺少 Position，已跳过");
                return;
            }

            MachineSide side = device.FrontFace
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

        /// <summary>
        /// 将 Mep Device DTO 列表批量转换为 Pocket Feature
        /// </summary>
        private static void ConvertDeviceToFeature(
            List<BimMepDeviceDtoV001>? devices, MomWall momWallData)
        {
            if (devices == null || devices.Count == 0) return;

            foreach (var device in devices)
                ConvertDeviceToFeature(device, momWallData);
        }

        /// <summary>
        /// 将单个 BimRebar DTO 转换为若干 RebarSlot Feature 并加入 MomWall
        /// </summary>
        private static void ConvertRebarSlotToFeature(
            BimRebarDtoV001? rebar, MomWall momWallData)
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
                        $"[WARN] Rebar (Pn={rebar.Pn ?? "noPn"}) Rod[{i}] " +
                        $"起终点缺失，已跳过");
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
        /// 将单个开洞 DTO 转换为 Window Feature
        /// </summary>
        private static void ConvertOpeningToFeature(
            BimOpeningHoleDtoV001? opening, MomWall momWallData)
        {
            if (opening == null) return;

            if (opening.Contour == null || opening.Contour.Count < 3)
            {
                Console.WriteLine(
                    $"[WARN] Opening (Uuid={opening.Uuid ?? "noUuid"}) " +
                    $"轮廓点不足 3 个，无法构成多边形，已跳过");
                return;
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
                return;
            }

            string id = string.IsNullOrWhiteSpace(opening.Uuid)
                ? $"Window-{momWallData.Features.Count(f => f.Type == FeatureType.Window):D2}"
                : $"Window-{opening.Uuid}";

            MachineSide side = MachineSide.Top;
            float depth = momWallData.Thickness;

            var window = new Window(
                id: id,
                side: side,
                contour: contour,
                depth: depth);

            momWallData.Features.Add(window);
        }

        /// <summary>
        /// 将开洞 DTO 列表批量转换为 Window Feature
        /// </summary>
        private static void ConvertOpeningToFeature(
            List<BimOpeningHoleDtoV001>? openings, MomWall momWallData)
        {
            if (openings == null || openings.Count == 0) return;

            foreach (var opening in openings)
                ConvertOpeningToFeature(opening, momWallData);
        }

        /// <summary>
        /// 将 ProppingConnectors DTO 转换为 Propping Feature
        /// V001 新增：使用 proppingConnectors 结构
        /// </summary>
        private static void ConvertProppingToFeature(
            BimProppingConnectorsDtoV001? proppingConnectors,
            MomWall momWallData)
        {
            if (proppingConnectors == null) return;

            int connectorIndex = 0;

            // 遍历所有类型的连接件
            ConvertProppingItemsToFeature(proppingConnectors.ColumnBracket, "ColumnBracket", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.Standard, "Standard", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TopBracket, "TopBracket", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TypeA, "TypeA", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TypeB, "TypeB", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TypeC, "TypeC", momWallData, ref connectorIndex);
            ConvertProppingItemsToFeature(proppingConnectors.TypeD, "TypeD", momWallData, ref connectorIndex);
        }

        private static void ConvertProppingItemsToFeature(
            List<BimProppingConnectorItemDtoV001>? items,
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
