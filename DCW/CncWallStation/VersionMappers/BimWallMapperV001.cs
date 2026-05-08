using BimWallData.V000;
using CncWallStation.Consts;
using CncWallStation.Features;
using CncWallStation.Features.MepSlots;
using CncWallStation.MomWallData;
using CncWallStation.Transforms;
using Infrastructure.Maths;
using Newtonsoft.Json;
using static CncWallStation.Features.MepSlots.MepCableBuilderCmd;

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
        /// 处理规则：
        /// ┌─────────────┬──────────────────────────────────────────────────────┐
        /// │ Type        │ 处理方式                                              │
        /// ├─────────────┼──────────────────────────────────────────────────────┤
        /// │ (普通)      │ 直线段，深度18mm，宽度30mm                            │
        /// │ corner      │ R60 圆弧倒角（AB/BC 段 < 60mm 则跳过）                │
        /// │ waffleBox   │ 只能是端点，从端点向内 100mm 段宽度变为 100mm          │
        /// │ device      │ 只能是端点，从端点向内 60mm 段深度渐变 60mm→18mm      │
        /// └─────────────┴──────────────────────────────────────────────────────┘
        /// </summary>
        private static void ConvertMepCableToFeature(
            List<BimMepCableDtoV000?> mepCables,   // ← 修正：List<T> 而非 List<T?>
            MomWall momWallData)
        {
            if (mepCables == null || mepCables.Count == 0) return;

            foreach (var mepCable in mepCables)
            {
                ConvertSingleMepCable(mepCable, momWallData);
            }
        }

        /// <summary>处理单条 MepCable（原方法体完整保留，改名避免递归混淆）</summary>
        private static void ConvertSingleMepCable(
            BimMepCableDtoV000? mepCable,
            MomWall momWallData)
        {
            if (mepCable == null) return;
            if (mepCable.Points == null || mepCable.Points.Count < 2) return;

            // ── 过滤无效点 ───────────────────────────────────────────────────
            var pts = mepCable.Points
                .Where(p => p.Position != null)
                .ToList();

            if (pts.Count < 2) return;

            // ── 验证 waffleBox / device 只能为端点 ───────────────────────────
            for (int i = 1; i < pts.Count - 1; i++)
            {
                string? t = pts[i].Type?.ToLowerInvariant();
                if (t == "wafflebox")
                    throw new InvalidOperationException(
                        $"[MepCable {mepCable.Pn}] waffleBox 点(index={i})不是端点");
                if (t == "device")
                    throw new InvalidOperationException(
                        $"[MepCable {mepCable.Pn}] device 点(index={i})不是端点");
            }

            // ── 加工面 ───────────────────────────────────────────────────────
            bool isFront = pts[0].FrontFace;
            MachineSide side = isFront ? MachineSide.Top : MachineSide.Bottom;

            // ── 特征 ID ──────────────────────────────────────────────────────
            string id = string.IsNullOrWhiteSpace(mepCable.Pn)
                ? $"MepCable-{mepCable.Hash}"
                : $"MepCable-{mepCable.Pn}";

            // ── 将 DTO 点转为 Vec2 及 Type ────────────────────────────────────
            var positions = pts
                .Select(p => new Vec2((float)p.Position!.X, (float)p.Position.Y))
                .ToArray();

            var types = pts
                .Select(p => p.Type?.ToLowerInvariant() ?? "")
                .ToArray();

            int n = positions.Length;

            // ── Step1: 构造原始折线段列表 ────────────────────────────────────
            var rawLines = new List<RawLine>();
            for (int i = 0; i < n - 1; i++)
                rawLines.Add(new RawLine(positions[i], positions[i + 1], WallConstants.MepCableSlotDepth));

            // ── Step2/3: 处理 corner 倒角 ────────────────────────────────────
            var buildCmds = new List<IBuildCmd>();
            ProcessCorners(rawLines, positions, types, WallConstants.MepCableSlotCornerRadius, buildCmds);

            // ── Step4: 两端特殊段覆盖 ────────────────────────────────────────
            string startType = types[0];
            string endType = types[n - 1];

            ApplyEndType(buildCmds, startType, isStartEnd: true,
                         WallConstants.WaffleBoxLength, WallConstants.WaffleBoxWidth, WallConstants.MepCableSlotWidth,
                         WallConstants.DeviceTaperLen, WallConstants.DeviceDepth, WallConstants.MepCableSlotDepth);

            ApplyEndType(buildCmds, endType, isStartEnd: false,
                         WallConstants.WaffleBoxLength, WallConstants.WaffleBoxWidth, WallConstants.MepCableSlotWidth,
                         WallConstants.DeviceTaperLen, WallConstants.DeviceDepth, WallConstants.MepCableSlotDepth);

            // ── Step5: 写入 MepSlot ──────────────────────────────────────────
            if (buildCmds.Count == 0) return;

            var slot = momWallData.AddMepSlot(id, side, width: WallConstants.MepCableSlotWidth);
            bool first = true;

            foreach (var cmd in buildCmds)
            {
                switch (cmd)
                {
                    case CmdLine cl:
                    if (first) { slot.AddLine(cl.Start, cl.End, cl.Depth); first = false; }
                    else { slot.LineTo(cl.End, cl.Depth); }
                    break;

                    case CmdWideLine cwl:
                    if (first) { slot.AddLine(cwl.Start, cwl.End, cwl.Depth); first = false; }
                    else { slot.LineTo(cwl.End, cwl.Depth); }
                    slot.Segments[^1].OverrideWidth = cwl.Width;
                    break;

                    case CmdTaperLine ctl:
                    BuildTaperLines(slot, ctl, first);
                    first = false;
                    break;

                    case CmdArc ca:
                    slot.AddArc(ca.Center, ca.Radius,
                                ca.StartAngleDeg, ca.EndAngleDeg,
                                ca.Depth, ca.IsClockwise);
                    if (first) first = false;
                    break;
                }
            }
        }


        // ═══════════════════════════════════════════════════════════════════════
        // Step3 实现：corner 倒角处理
        // ═══════════════════════════════════════════════════════════════════════

        private static void ProcessCorners(
    List<RawLine> rawLines,
    Vec2[] positions,
    string[] types,
    float radius,
    List<IBuildCmd> buildCmds)
        {
            int edgeCount = rawLines.Count;

            var trimStart = new float[edgeCount];
            var trimEnd = new float[edgeCount];

            // ── 预计算各 corner 点的倒角截断量 ──────────────────────────────
            for (int i = 1; i < positions.Length - 1; i++)
            {
                if (types[i] != "corner") continue;

                float lenAB = (positions[i] - positions[i - 1]).Length();
                float lenBC = (positions[i + 1] - positions[i]).Length();

                if (lenAB < radius || lenBC < radius) continue;

                trimEnd[i - 1] = radius;
                trimStart[i] = radius;
            }

            // ── 依次生成段命令 ───────────────────────────────────────────────
            for (int i = 0; i < edgeCount; i++)
            {
                var edge = rawLines[i];
                Vec2 rawDir = edge.End - edge.Start;
                float len = rawDir.Length();
                Vec2 dir = len < 1e-6f ? Vec2.Zero : rawDir * (1f / len); // ← Normalize()

                float ts = trimStart[i];
                float te = trimEnd[i];

                Vec2 segStart = edge.Start + dir * ts;
                Vec2 segEnd = edge.End - dir * te;
                float segLen = len - ts - te;

                if (segLen > 0.001f)
                {
                    buildCmds.Add(new CmdLine(segStart, segEnd, edge.Depth));
                }

                // ── corner 圆弧 ──────────────────────────────────────────────
                int cornerIdx = i + 1;
                if (cornerIdx < positions.Length - 1
                    && types[cornerIdx] == "corner"
                    && trimEnd[i] > 0f)
                {
                    Vec2 arcStart = edge.End - dir * te;

                    Vec2 nextRawDir = rawLines[i + 1].End - rawLines[i + 1].Start;
                    float nextLen = nextRawDir.Length();
                    Vec2 nextDir = nextLen < 1e-6f
                                           ? Vec2.Zero
                                           : nextRawDir * (1f / nextLen); // ← Normalize()

                    Vec2 arcEnd = rawLines[i + 1].Start + nextDir * trimStart[i + 1];
                    Vec2 cornerPt = positions[cornerIdx];

                    // d1 / d2：从 corner 点指向圆弧两端
                    Vec2 d1 = arcStart - cornerPt;
                    float d1Len = d1.Length();
                    Vec2 d1Norm = d1Len < 1e-6f ? Vec2.Zero : d1 * (1f / d1Len);

                    Vec2 d2 = arcEnd - cornerPt;
                    float d2Len = d2.Length();
                    Vec2 d2Norm = d2Len < 1e-6f ? Vec2.Zero : d2 * (1f / d2Len);

                    // ── 点积改为实例方法调用 ─────────────────────────────────
                    float cosAngle = d1Norm.Dot(d2Norm);                         // ← 实例方法
                    float clamped = MathF.Max(-1f, MathF.Min(1f, cosAngle));    // ← Math.Clamp 替换
                    float halfAngle = MathF.Acos(clamped) * 0.5f;

                    float distToCenter = halfAngle < 0.001f
                        ? radius
                        : radius / MathF.Sin(halfAngle);

                    Vec2 bisector = d1Norm + d2Norm;
                    float bisLen = bisector.Length();
                    Vec2 bisNorm = bisLen < 1e-6f ? Vec2.Zero : bisector * (1f / bisLen);

                    Vec2 center = cornerPt + bisNorm * distToCenter;

                    float startAngle = MathF.Atan2(
                        arcStart.Y - center.Y,
                        arcStart.X - center.X) * (180f / MathF.PI);

                    float endAngle = MathF.Atan2(
                        arcEnd.Y - center.Y,
                        arcEnd.X - center.X) * (180f / MathF.PI);

                    // ── 叉积改为实例方法调用 ─────────────────────────────────
                    float cross = d1Norm.Cross(d2Norm);                        // ← 实例方法
                    bool isCw = cross > 0;

                    buildCmds.Add(new CmdArc(
                        center, radius,
                        startAngle, endAngle,
                        edge.Depth, isCw));
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Step4 实现：两端特殊段覆盖
        // ═══════════════════════════════════════════════════════════════════════

        private static void ApplyEndType(
            List<IBuildCmd> cmds,
            string endType,
            bool isStartEnd,          // true = 路径起点端；false = 路径终点端
            float waffleLen, float waffleWidth, float normalWidth,
            float deviceTaperLen, float deviceDepth, float normalDepth)
        {
            if (cmds.Count == 0) return;
            if (endType != "wafflebox" && endType != "device") return;

            // ── 定位目标端的第一个（或最后一个）直线段 ──────────────────────
            // 从对应端向内找第一个 CmdLine，然后拆分

            if (isStartEnd)
            {
                // 从 cmds[0] 开始找
                int idx = cmds.FindIndex(c => c is CmdLine);
                if (idx < 0) return;

                var line = (CmdLine)cmds[idx];
                Vec2 dir = (line.End - line.Start).Normalize();
                float len = (line.End - line.Start).Length();
                float cutLen = endType == "wafflebox" ? waffleLen : deviceTaperLen;

                if (len <= cutLen)
                {
                    // 整段替换
                    ReplaceCmd(cmds, idx, endType,
                               line.Start, line.End,
                               waffleWidth, normalDepth, deviceDepth, isStartEnd);
                }
                else
                {
                    // 截断：前 cutLen 为特殊段，剩余为普通段
                    Vec2 cutPt = line.Start + dir * cutLen;

                    ReplaceCmd(cmds, idx, endType,
                               line.Start, cutPt,
                               waffleWidth, normalDepth, deviceDepth, isStartEnd);

                    // 剩余段插入 cutPt → line.End
                    cmds.Insert(idx + 1, new CmdLine(cutPt, line.End, line.Depth));
                }
            }
            else
            {
                // 从 cmds[^1] 开始反向找
                int idx = -1;
                for (int i = cmds.Count - 1; i >= 0; i--)
                {
                    if (cmds[i] is CmdLine) { idx = i; break; }
                }
                if (idx < 0) return;

                var line = (CmdLine)cmds[idx];
                Vec2 dir = (line.Start - line.End).Normalize(); // 反向
                float len = (line.End - line.Start).Length();
                float cutLen = endType == "wafflebox" ? waffleLen : deviceTaperLen;

                if (len <= cutLen)
                {
                    ReplaceCmd(cmds, idx, endType,
                               line.Start, line.End,
                               waffleWidth, normalDepth, deviceDepth, isStartEnd);
                }
                else
                {
                    // 截断：末尾 cutLen 为特殊段，剩余为普通段
                    Vec2 cutPt = line.End + dir * cutLen; // 从终点向起点方向

                    // 普通段：line.Start → cutPt
                    cmds[idx] = new CmdLine(line.Start, cutPt, line.Depth);

                    // 特殊段：cutPt → line.End
                    ReplaceCmd(cmds, idx + 1, endType,
                               cutPt, line.End,
                               waffleWidth, normalDepth, deviceDepth, isStartEnd,
                               insert: true);
                }
            }
        }

        /// <summary>将指定位置替换（或插入）为特殊段命令</summary>
        private static void ReplaceCmd(
            List<IBuildCmd> cmds, int idx, string endType,
            Vec2 start, Vec2 end,
            float waffleWidth, float normalDepth, float deviceDepth,
            bool isStartEnd, bool insert = false)
        {
            IBuildCmd newCmd = endType switch
            {
                "wafflebox" => new CmdWideLine(start, end, normalDepth, waffleWidth),

                // device：渐变方向 = 从端点（深度60mm）向内（深度18mm）
                "device" => isStartEnd
                    ? new CmdTaperLine(start, end, deviceDepth, normalDepth)  // 起点端：60→18
                    : new CmdTaperLine(start, end, normalDepth, deviceDepth), // 终点端：18→60

                _ => new CmdLine(start, end, normalDepth)
            };

            if (insert)
                cmds.Insert(idx, newCmd);
            else if (idx < cmds.Count)
                cmds[idx] = newCmd;
            else
                cmds.Add(newCmd);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Step5 辅助：渐变深度段拆分（10 步模拟）
        // ═══════════════════════════════════════════════════════════════════════

        private static void BuildTaperLines(
            MepSlot slot, CmdTaperLine ctl, bool isFirst,
            int steps = 10)
        {
            Vec2 dir = (ctl.End - ctl.Start).Normalize();
            float len = (ctl.End - ctl.Start).Length();
            float stepLen = len / steps;

            for (int s = 0; s < steps; s++)
            {
                float t0 = (float)s / steps;
                float t1 = (float)(s + 1) / steps;

                Vec2 segStart = ctl.Start + dir * (t0 * len);
                Vec2 segEnd = ctl.Start + dir * (t1 * len);

                // 取段中点深度（线性插值）
                float depth = ctl.DepthStart + (ctl.DepthEnd - ctl.DepthStart) * ((t0 + t1) * 0.5f);

                if (isFirst && s == 0)
                    slot.AddLine(segStart, segEnd, depth);
                else
                    slot.LineTo(segEnd, depth);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 中间数据结构
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>原始折线段（未处理圆弧倒角）</summary>
        private record RawLine(Vec2 Start, Vec2 End, float Depth);




        /*
         * BimMepCablePointXyDto[]
            │
            ▼
      过滤无效点 + 端点类型验证（waffleBox/device 只能在两端）
            │
            ▼
      rawLines[]（n-1 条原始折线段，深度均为 NormalDepth）
            │
            ▼
      ProcessCorners() ──► buildCmds[]
      • corner 点：截断前后边 R60，插入 CmdArc
      • 其余点：直接 CmdLine
            │
            ▼
      ApplyEndType() × 2（起点端 + 终点端）
      • waffleBox → CmdWideLine（宽100mm）
      • device    → CmdTaperLine（深度渐变60→18mm）
            │
            ▼
      遍历 buildCmds → MepSlot.AddLine / AddArc / LineTo
      • CmdTaperLine → 拆成10小段模拟渐变
         */
    }
}
