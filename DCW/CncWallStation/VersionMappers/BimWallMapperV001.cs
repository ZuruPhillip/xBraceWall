using BimWallData.V000;
using CncWallStation.Consts;
using CncWallStation.Features;
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

            //生成BindingKey数据
            return momWallData;
        }

        /// <summary>
        /// 将 SteelFrameColumns DTO 列表转换为 Groove Feature 列表
        /// </summary>
        public static void ConvertSteelColumnsToFeatures(
            List<BimSteelFrameColumnDtoV000>? steelFrameColumns, MomWall momWallData)
        {
            if (steelFrameColumns == null || steelFrameColumns.Count == 0)
                return ;

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
        public static void ConvertTopPlateToFeature(
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
    }
}
