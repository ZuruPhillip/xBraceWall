using BimWallData.V000;
using CncWallStation.Consts;
using CncWallStation.Features;
using CncWallStation.Features.MepSlots;
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

            //生成胶水密封槽数据TopPlateGroove
            GenerateGlueSealFeature(momWallData);

            //生成BendingKey数据
            ConvertBendingKeyToFeature(dto.BendingKeys, momWallData);

            //生成MepCableSlot数据
            ConvertMepCableToFeature(dto.MepCables, momWallData);

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
                side: MachineSide.Bottom,
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

    }
}
