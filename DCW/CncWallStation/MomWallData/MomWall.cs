using CncWallStation.Features;
using CncWallStation.Features.MepSlots;
using CncWallStation.Transforms;
using Infrastructure.Maths;
using Newtonsoft.Json;
using System.Text;

namespace CncWallStation.MomWallData
{
    /// <summary>
    /// 墙体实体
    /// ┌─────────────────────────────────────────┐
    /// │ 几何：俯视图多边形轮廓 + 统一厚度          │
    /// │ 特征：切槽 / 开孔 / 挖坑（含加工面）       │
    /// │ 变换：平移 + 四元数旋转（支持撤销）         │
    /// │ 翻面：坐标自动重映射，原点始终为左下角      │
    /// └─────────────────────────────────────────┘
    /// </summary>
    public class MomWall
    {
        // ══════════════════════════════════════════════
        // 基本属性
        // ══════════════════════════════════════════════

        public string Id { get; set; }
        public string Material { get; set; }
        public string Remark { get; set; } = string.Empty;

        // ══════════════════════════════════════════════
        // 几何定义
        // ══════════════════════════════════════════════

        public List<Vec2> Outline { get; private set; }
        public float Thickness { get; set; }
        public float BaseElevation { get; set; }

        // ══════════════════════════════════════════════
        // 计算尺寸（AABB / OBB，由轮廓推算）
        // ══════════════════════════════════════════════

        /// <summary>轮廓 X 方向跨度（AABB，mm）</summary>
        public float Length { get; private set; }

        /// <summary>轮廓 Y 方向跨度（AABB，mm）</summary>
        public float Width { get; private set; }

        /// <summary>OBB 长轴长度（mm）</summary>
        [JsonIgnore]
        public float ObbLength { get; private set; }
        [JsonIgnore]
        /// <summary>OBB 短轴长度（mm）</summary>
        public float ObbWidth { get; private set; }
        [JsonIgnore]
        /// <summary>OBB 长轴与 X 轴夹角（度）</summary>
        public float ObbAngleDeg { get; private set; }

        // ══════════════════════════════════════════════
        // 【新增】实际尺寸
        // ══════════════════════════════════════════════

        /// <summary>
        /// 实际长度（mm）
        /// 默认等于 <see cref="Length"/>（AABB X 跨度）
        /// 可独立赋值以反映裁切/公差后的真实尺寸
        /// </summary>
        [JsonIgnore]
        public float ActualLength
        {
            get => _actualLength ?? Length;
            set => _actualLength = value;
        }
        private float? _actualLength;

        /// <summary>
        /// 实际宽度（mm）
        /// 默认等于 <see cref="Width"/>（AABB Y 跨度）
        /// </summary>
        [JsonIgnore]
        public float ActualWidth
        {
            get => _actualWidth ?? Width;
            set => _actualWidth = value;
        }
        private float? _actualWidth;

        /// <summary>
        /// 实际厚度（mm）
        /// 默认等于 <see cref="Thickness"/>
        /// </summary>
        [JsonIgnore]
        public float ActualThickness
        {
            get => _actualThickness ?? Thickness;
            set => _actualThickness = value;
        }
        private float? _actualThickness;

        /// <summary>
        /// 重置实际尺寸，使其重新跟随计算值
        /// </summary>
        public void ResetActualDimensions()
        {
            _actualLength = null;
            _actualWidth = null;
            _actualThickness = null;
        }

        /// <summary>
        /// 实际尺寸是否已被手动覆盖（任意一项）
        /// </summary>
        [JsonIgnore]
        public bool IsActualDimensionOverridden =>
            _actualLength.HasValue ||
            _actualWidth.HasValue ||
            _actualThickness.HasValue;

        // ══════════════════════════════════════════════
        // 【新增】基准点 PivotPoint
        // ══════════════════════════════════════════════

        /// <summary>
        /// 墙体基准点（局部坐标系，mm）
        /// 默认 = 轮廓左下角（minX, minY）+ BaseElevation
        ///
        /// 用途：
        ///   - 旋转中心参考点
        ///   - CNC 加工原点对齐
        ///   - BIM 定位锚点
        ///
        /// 赋 null 可重置为默认左下角自动计算值
        /// </summary>
        public Vec3 PivotPoint
        {
            get
            {
                if (_pivotPoint.HasValue)
                    return _pivotPoint.Value;

                // 默认：轮廓 AABB 左下角 + BaseElevation
                var (minX, minY, _, _) = GetOutlineBounds();
                return new Vec3(minX, minY, BaseElevation);
            }
            set => _pivotPoint = value;
        }
        private Vec3? _pivotPoint;

        /// <summary>
        /// 重置基准点，使其重新跟随左下角自动计算
        /// </summary>
        public void ResetPivotPoint() => _pivotPoint = null;

        /// <summary>
        /// 基准点是否已被手动覆盖
        /// </summary>
        [JsonIgnore]
        public bool IsPivotOverridden => _pivotPoint.HasValue;

        // ══════════════════════════════════════════════
        // 加工特征
        // ══════════════════════════════════════════════

        public List<Feature> Features { get; private set; } = new List<Feature>();

        // ══════════════════════════════════════════════
        // 空间变换
        // ══════════════════════════════════════════════

        public Transform3D Transform { get; private set; } = new Transform3D();
        private readonly Stack<Transform3D> _transformHistory = new Stack<Transform3D>();

        // ══════════════════════════════════════════════
        // 翻面状态
        // ══════════════════════════════════════════════

        public Vec2 MachineOrigin { get; private set; } = Vec2.Zero;
        public int FlipCount { get; private set; } = 0;
        private readonly Stack<FlipSnapshot> _flipHistory = new Stack<FlipSnapshot>();

        // ══════════════════════════════════════════════
        // 包围盒缓存
        // ══════════════════════════════════════════════

        private bool _bboxDirty = true;
        private Vec3 _bboxMin, _bboxMax;

        // ══════════════════════════════════════════════
        // 构造函数
        // ══════════════════════════════════════════════

        /// <summary>
        /// 构造墙体
        /// 自动计算 Length / Width（AABB）及 OBB 方向尺寸
        /// PivotPoint 默认为轮廓左下角（minX, minY, BaseElevation）
        /// </summary>
        public MomWall(string id,
                       IEnumerable<Vec2> outline,
                       float thickness,
                       float baseElevation = 0f,
                       string material = "AAC")
        {
            Id = id;
            Material = material;
            Outline = new List<Vec2>(outline);
            Thickness = thickness;
            BaseElevation = baseElevation;

            // 计算 AABB / OBB 尺寸（同时为 PivotPoint 默认值做准备）
            RecalculateDimensions();
        }

        // ══════════════════════════════════════════════
        // 尺寸计算
        // ══════════════════════════════════════════════

        /// <summary>
        /// 重新计算平面尺寸（AABB + OBB）
        /// 调用时机：构造 / Flip / UndoFlip / 外部修改 Outline 后
        /// 注意：手动覆盖的 ActualLength/Width/Thickness 不受影响
        ///       PivotPoint 若未被手动覆盖，下次访问时自动跟随新轮廓
        /// </summary>
        public void RecalculateDimensions()
        {
            if (Outline == null || Outline.Count == 0)
            {
                Length = Width = ObbLength = ObbWidth = ObbAngleDeg = 0f;
                return;
            }
            ComputeAabbDimensions();
            ComputeObbDimensions();
        }

        private void ComputeAabbDimensions()
        {
            var (minX, minY, maxX, maxY) = GetOutlineBounds();
            Length = maxX - minX;
            Width = maxY - minY;
        }

        private void ComputeObbDimensions()
        {
            if (Outline.Count < 3)
            {
                ObbLength = Length; ObbWidth = Width; ObbAngleDeg = 0f;
                return;
            }

            var hull = ComputeConvexHull(Outline);
            if (hull.Count < 2)
            {
                ObbLength = Length; ObbWidth = Width; ObbAngleDeg = 0f;
                return;
            }

            float minArea = float.MaxValue;
            float bestLen = Length, bestWid = Width, bestAngle = 0f;
            int n = hull.Count;

            for (int i = 0; i < n; i++)
            {
                Vec2 edgeDir = (hull[(i + 1) % n] - hull[i]).Normalize();
                Vec2 perpDir = new Vec2(-edgeDir.Y, edgeDir.X);

                float minP1 = float.MaxValue, maxP1 = float.MinValue;
                float minP2 = float.MaxValue, maxP2 = float.MinValue;

                foreach (var pt in hull)
                {
                    float p1 = pt.Dot(edgeDir);
                    float p2 = pt.Dot(perpDir);
                    if (p1 < minP1) minP1 = p1;
                    if (p1 > maxP1) maxP1 = p1;
                    if (p2 < minP2) minP2 = p2;
                    if (p2 > maxP2) maxP2 = p2;
                }

                float spanLen = maxP1 - minP1;
                float spanWid = maxP2 - minP2;
                float area = spanLen * spanWid;

                if (area < minArea)
                {
                    minArea = area;
                    bestLen = spanLen;
                    bestWid = spanWid;
                    bestAngle = MathF.Atan2(edgeDir.Y, edgeDir.X) * 180f / MathF.PI;
                }
            }

            ObbLength = MathF.Max(bestLen, bestWid);
            ObbWidth = MathF.Min(bestLen, bestWid);
            ObbAngleDeg = bestAngle;
        }

        private static List<Vec2> ComputeConvexHull(List<Vec2> points)
        {
            int n = points.Count;
            if (n < 3) return new List<Vec2>(points);

            var sorted = points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            Vec2 pivot = sorted[0];

            var rest = sorted
                .Skip(1)
                .OrderBy(p => MathF.Atan2(p.Y - pivot.Y, p.X - pivot.X))
                .ThenBy(p => (p - pivot).LengthSquared())
                .ToList();

            var stack = new List<Vec2> { pivot };
            foreach (var pt in rest)
            {
                while (stack.Count >= 2)
                {
                    Vec2 a = stack[^2], b = stack[^1];
                    if ((b - a).Cross(pt - a) <= 0f)
                        stack.RemoveAt(stack.Count - 1);
                    else
                        break;
                }
                stack.Add(pt);
            }
            return stack;
        }

        // ══════════════════════════════════════════════
        // 几何属性
        // ══════════════════════════════════════════════

        public float OutlineArea()
        {
            float area = 0f;
            int n = Outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vec2 a = Outline[i];
                Vec2 b = Outline[(i + 1) % n];
                area += a.Cross(b);
            }
            return MathF.Abs(area) * 0.5f;
        }

        public float Volume() => OutlineArea() * Thickness;

        public (float minX, float minY, float maxX, float maxY) GetOutlineBounds()
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in Outline)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return (minX, minY, maxX, maxY);
        }

        public bool ContainsPoint(Vec2 pt)
        {
            int n = Outline.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vec2 pi = Outline[i], pj = Outline[j];
                if ((pi.Y > pt.Y) != (pj.Y > pt.Y) &&
                    pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y) + pi.X)
                    inside = !inside;
            }
            return inside;
        }

        // ══════════════════════════════════════════════
        // 特征管理（省略，与原版完全一致）
        // ══════════════════════════════════════════════

        public MomWall AddGroove(string id, MachineSide side,
                                 Vec2 startPt, Vec2 endPt,
                                 float width, float depth)
        {
            Features.Add(new Groove(id, side, startPt, endPt, width, depth));
            return this;
        }

        public Groove AddGroove(string id, MachineSide side,
                                Vec2 startPt, Vec2 endPt,
                                float width, float depth,
                                GrooveType grooveType = GrooveType.General)
        {
            var g = new Groove(id, side, startPt, endPt, width, depth, grooveType);
            Features.Add(g);
            return g;
        }

        public Groove AddAsymmetricGroove(string id, MachineSide side,
                                          Vec2 startPt, Vec2 endPt,
                                          float leftWidth, float rightWidth,
                                          float depth,
                                          GrooveType grooveType = GrooveType.General)
        {
            var g = new Groove(id, side, startPt, endPt,
                               leftWidth, rightWidth, depth, grooveType);
            Features.Add(g);
            return g;
        }

        public Groove AddSteelColumnGroove(string id, MachineSide side,
                                           Vec2 startPt, Vec2 endPt,
                                           float width, float depth)
            => AddGroove(id, side, startPt, endPt, width, depth, GrooveType.SteelColumn);

        public Groove AddTopPlateGroove(string id, MachineSide side,
                                        Vec2 startPt, Vec2 endPt,
                                        float width, float depth)
            => AddGroove(id, side, startPt, endPt, width, depth, GrooveType.TopPlate);

        public Groove AddXBraceSteelGroove(string id, MachineSide side,
                                           Vec2 startPt, Vec2 endPt,
                                           float leftWidth, float rightWidth,
                                           float depth)
            => AddAsymmetricGroove(id, side, startPt, endPt,
                                   leftWidth, rightWidth, depth, GrooveType.XBraceSteel);

        public MomWall AddHole(string id, MachineSide side,
                               Vec2 center, float radius, float depth,
                               bool throughHole = false)
        {
            Features.Add(new Hole(id, side, center, radius, depth, throughHole));
            return this;
        }

        public MomWall AddPocket(string id, MachineSide side,
                                 Vec2 center, float width, float height,
                                 float depth, float cornerRadius = 0f)
        {
            Features.Add(new Pocket(id, side, center, width, height,
                                    depth, cornerRadius));
            return this;
        }

        public MepSlot AddMepSlot(string id, MachineSide side, float width)
        {
            var slot = new MepSlot(id, side, width);
            Features.Add(slot);
            return slot;
        }

        public bool RemoveFeature(string id)
        {
            var f = Features.FirstOrDefault(x => x.Id == id);
            return f != null && Features.Remove(f);
        }

        public IEnumerable<Feature> GetFeaturesByType(FeatureType type) =>
            Features.Where(f => f.Type == type);

        public IEnumerable<Feature> GetFeaturesByInitialSide(MachineSide side) =>
            Features.Where(f => f.InitialSide == side);

        public IEnumerable<Feature> GetFeaturesByCurrentSide(MachineSide side) =>
            Features.Where(f => f.CurrentSide == side);

        public IEnumerable<Feature> GetAccessibleFeatures() =>
            Features.Where(f => f.Face.IsAccessible);

        // ══════════════════════════════════════════════
        // 空间变换
        // ══════════════════════════════════════════════

        public MomWall Translate(Vec3 delta)
        {
            _transformHistory.Push(Transform.Clone());
            Transform.Translation = Transform.Translation + delta;
            _bboxDirty = true;
            return this;
        }

        public MomWall Rotate(Vec3 axis, float angleDeg, Vec3? pivot = null)
        {
            _transformHistory.Push(Transform.Clone());
            float rad = angleDeg * MathF.PI / 180f;
            Quaternion q = Quaternion.FromAxisAngle(axis, rad);
            Transform.Pivot = pivot ?? Vec3.Zero;
            Transform.Rotation = (q * Transform.Rotation).Normalize();
            foreach (var f in Features) f.ApplyRotation(q);
            _bboxDirty = true;
            return this;
        }

        public bool UndoTransform()
        {
            if (_transformHistory.Count == 0) return false;
            Transform = _transformHistory.Pop();
            _bboxDirty = true;
            RebuildFeatureNormals();
            return true;
        }

        public MomWall ResetTransform()
        {
            _transformHistory.Clear();
            Transform = new Transform3D();
            foreach (var f in Features) f.ResetFace();
            _bboxDirty = true;
            return this;
        }

        public int UndoTransformSteps => _transformHistory.Count;

        private void RebuildFeatureNormals()
        {
            foreach (var f in Features)
            {
                f.ResetFace();
                f.ApplyRotation(Transform.Rotation);
            }
        }

        // ══════════════════════════════════════════════
        // 翻面操作
        // ══════════════════════════════════════════════

        public MomWall Flip(FlipAxis axis = FlipAxis.AroundX)
        {
            var bounds = GetOutlineBounds();

            _flipHistory.Push(FlipSnapshot.Capture(
                Outline, Features, MachineOrigin, FlipCount,
                _pivotPoint, _actualLength, _actualWidth, _actualThickness));

            for (int i = 0; i < Outline.Count; i++)
                Outline[i] = FlipRemapper.RemapPoint(Outline[i], axis, bounds);

            foreach (var f in Features)
                f.ApplyFlip(axis, bounds);

            var nb = GetOutlineBounds();
            MachineOrigin = new Vec2(nb.minX, nb.minY);
            FlipCount++;
            _bboxDirty = true;

            RecalculateDimensions();

            // PivotPoint 若未手动覆盖，自动跟随新左下角（无需额外处理）
            // 若已手动覆盖，翻面后同步做镜像重映射
            if (_pivotPoint.HasValue)
                _pivotPoint = RemapPivotOnFlip(_pivotPoint.Value, axis, bounds);

            return this;
        }

        public bool UndoFlip()
        {
            if (_flipHistory.Count == 0) return false;
            _flipHistory.Pop().Restore(this);
            _bboxDirty = true;
            RecalculateDimensions();
            return true;
        }

        public int UndoFlipSteps => _flipHistory.Count;

        /// <summary>翻面时重映射手动设置的 PivotPoint</summary>
        private static Vec3 RemapPivotOnFlip(
            Vec3 pivot,
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            return axis switch
            {
                // 绕 X 翻面：Y 坐标镜像
                FlipAxis.AroundX => new Vec3(
                    pivot.X,
                    bounds.minY + (bounds.maxY - pivot.Y),
                    pivot.Z),

                // 绕 Y 翻面：X 坐标镜像
                FlipAxis.AroundY => new Vec3(
                    bounds.minX + (bounds.maxX - pivot.X),
                    pivot.Y,
                    pivot.Z),

                _ => pivot
            };
        }

        // ══════════════════════════════════════════════
        // 世界坐标计算
        // ══════════════════════════════════════════════

        public List<Vec3> GetWorldVertices()
        {
            var result = new List<Vec3>(Outline.Count * 2);
            foreach (var p in Outline)
            {
                result.Add(Transform.Apply(new Vec3(p.X, p.Y, BaseElevation)));
                result.Add(Transform.Apply(new Vec3(p.X, p.Y, BaseElevation + Thickness)));
            }
            return result;
        }

        public Vec3 GetFeatureWorldPos(Feature feature) =>
            Transform.Apply(new Vec3(
                feature.LocalPos.X,
                feature.LocalPos.Y,
                BaseElevation));

        public IEnumerable<(Feature Feature, Vec3 WorldPos)> GetFeaturesWorldPos() =>
            Features.Select(f => (f, GetFeatureWorldPos(f)));

        /// <summary>获取 PivotPoint 的世界坐标（含变换）</summary>
        public Vec3 GetPivotWorldPos() => Transform.Apply(PivotPoint);

        // ══════════════════════════════════════════════
        // 包围盒 AABB
        // ══════════════════════════════════════════════

        public (Vec3 Min, Vec3 Max) GetBoundingBox()
        {
            if (_bboxDirty) ComputeBBox();
            return (_bboxMin, _bboxMax);
        }

        private void ComputeBBox()
        {
            var verts = GetWorldVertices();
            if (verts.Count == 0) return;
            _bboxMin = _bboxMax = verts[0];
            foreach (var v in verts)
            {
                _bboxMin = new Vec3(MathF.Min(_bboxMin.X, v.X),
                                    MathF.Min(_bboxMin.Y, v.Y),
                                    MathF.Min(_bboxMin.Z, v.Z));
                _bboxMax = new Vec3(MathF.Max(_bboxMax.X, v.X),
                                    MathF.Max(_bboxMax.Y, v.Y),
                                    MathF.Max(_bboxMax.Z, v.Z));
            }
            _bboxDirty = false;
        }

        // ══════════════════════════════════════════════
        // 打印输出
        // ══════════════════════════════════════════════

        public void Print()
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════╗");
            sb.AppendLine($"║  Wall [{Id}]  材料: {Material}");
            sb.AppendLine($"║  轮廓顶点数  : {Outline.Count}");
            sb.AppendLine($"║  Thickness   : {Thickness}mm   底面高度: {BaseElevation}mm");
            sb.AppendLine($"║  ┌─ AABB     : L={Length:F2}  W={Width:F2}  H={Thickness:F2} mm");
            sb.AppendLine($"║  └─ OBB      : L={ObbLength:F2}  W={ObbWidth:F2}  " +
                          $"Angle={ObbAngleDeg:F2}°");
            // 实际尺寸
            string actualMark = IsActualDimensionOverridden ? "（已覆盖）" : "（跟随计算值）";
            sb.AppendLine($"║  实际尺寸{actualMark}");
            sb.AppendLine($"║    ActualLength    = {ActualLength:F2} mm");
            sb.AppendLine($"║    ActualWidth     = {ActualWidth:F2} mm");
            sb.AppendLine($"║    ActualThickness = {ActualThickness:F2} mm");
            // PivotPoint
            string pivotMark = IsPivotOverridden ? "（已手动设置）" : "（默认左下角）";
            sb.AppendLine($"║  PivotPoint {pivotMark}");
            sb.AppendLine($"║    Local  = {PivotPoint}");
            sb.AppendLine($"║    World  = {GetPivotWorldPos()}");
            sb.AppendLine($"║  加工原点    : {MachineOrigin}   已翻面: {FlipCount} 次");
            sb.AppendLine($"║  当前变换    : {Transform}");
            sb.AppendLine($"║  可撤销变换  : {UndoTransformSteps} 步   " +
                          $"可撤销翻面: {UndoFlipSteps} 步");
            sb.AppendLine("╠══════════════════════════════════════════════════╣");
            sb.AppendLine($"║  特征列表（共 {Features.Count} 个）");
            foreach (var f in Features)
                sb.AppendLine($"║    {f.GetInfo()}");
            var (bmin, bmax) = GetBoundingBox();
            sb.AppendLine("╠══════════════════════════════════════════════════╣");
            sb.AppendLine($"║  包围盒 Min : {bmin}");
            sb.AppendLine($"║  包围盒 Max : {bmax}");
            sb.AppendLine("╚══════════════════════════════════════════════════╝");
            Console.Write(sb);
        }

        /// <summary>
        /// 打印旋转/翻面后所有顶点和特征的世界坐标
        /// </summary>
        public void PrintWorldCoordinates(string label = "")
        {
            var sb = new StringBuilder();

            // ══════════════════════════════════════════════════════
            // 列宽定义
            // ══════════════════════════════════════════════════════
            const int W_IDX = 3;   // 顶点序号
            const int W_LOCAL = 18;   // 局部坐标
            const int W_BOTTOM = 26;   // 底面世界坐标
            const int W_TOP = 26;   // 顶面世界坐标

            const int W_FID = 6;   // 特征 ID
            const int W_FTYPE = 9;   // 特征类型
            const int W_INITSIDE = 12;   // 初始加工面
            const int W_CURSIDE = 12;   // 当前加工面
            const int W_FWORLD = 27;   // 特征世界坐标

            const int W_NID = 6;   // 法向量 ID
            const int W_NORMAL = 30;   // 法向量
            const int W_DIRECTION = 36;   // 朝向描述

            // ══════════════════════════════════════════════════════
            // 计算各子表内容宽度（不含最外层两侧 ║ 和空格）
            //   每列占：W + 2（两侧各1空格）+ 1（║分隔）
            //   最右列：W + 2（两侧各1空格，无║）
            // ══════════════════════════════════════════════════════
            //  顶点表：║_#_║_LOCAL_║_BOTTOM_║_TOP_║
            int vtxInner = (W_IDX + 2) + 1
                          + (W_LOCAL + 2) + 1
                          + (W_BOTTOM + 2) + 1
                          + (W_TOP + 2);

            //  特征表：║_FID_║_FTYPE_║_INIT_║_CUR_║_WORLD_║
            int ftInner = (W_FID + 2) + 1
                          + (W_FTYPE + 2) + 1
                          + (W_INITSIDE + 2) + 1
                          + (W_CURSIDE + 2) + 1
                          + (W_FWORLD + 2);

            //  法向量表：║_NID_║_NORMAL_║_DIR_║
            int nmInner = (W_NID + 2) + 1
                          + (W_NORMAL + 2) + 1
                          + (W_DIRECTION + 2);

            // 取最宽的子表作为全局内容宽度
            int IW = Math.Max(Math.Max(vtxInner, ftInner), nmInner);

            // ══════════════════════════════════════════════════════
            // 通用行构造辅助
            // ══════════════════════════════════════════════════════

            // 外框横线
            string OuterTop = $"╔{new string('═', IW + 2)}╗";
            string OuterBottom = $"╚{new string('═', IW + 2)}╝";
            string OuterMid = $"╠{new string('═', IW + 2)}╣";

            // 普通文本行（内容不足时自动右填空格，保证右边界对齐）
            string TextRow(string content)
                => $"║ {content.PadRight(IW)} ║";

            // 子表行（内容 + 末尾填充空格到 IW，保证右边界对齐）
            string TableRow(string rowContent)
            {
                // rowContent 是子表内部列格式，已含左侧空格
                // 需要补右侧空格使总内容宽度 = IW
                string padded = rowContent.PadRight(IW);
                return $"║{padded}║";
            }

            // 子表分隔线（╠...╣，总宽 = IW + 2）
            // 传入各列宽数组，自动构造内部 ╦/╬ 分隔
            string SubLine(char left, char mid, char right, int[] colWidths)
            {
                var parts = new System.Text.StringBuilder();
                parts.Append(left);
                for (int ci = 0; ci < colWidths.Length; ci++)
                {
                    parts.Append(new string('═', colWidths[ci] + 2));
                    parts.Append(ci < colWidths.Length - 1 ? mid : right);
                }
                // 补足到 IW + 2（含左右边界字符各1）
                int built = 1 + colWidths.Sum(w => w + 2 + 1); // left + cols + 右边界
                                                               // 右边界已含在上面循环末尾，不需额外处理
                return parts.ToString().PadRight(IW + 2)
                       .TrimEnd().PadRight(IW + 2); // 确保宽度
            }

            // 更可靠的子表分隔线构造（显式补足 ═ 到 IW+2）
            string MakeSubLine(char leftCap, char sep, char rightCap, int[] cols)
            {
                var sb2 = new System.Text.StringBuilder();
                sb2.Append(leftCap);
                for (int ci = 0; ci < cols.Length; ci++)
                {
                    sb2.Append(new string('═', cols[ci] + 2));
                    sb2.Append(ci < cols.Length - 1 ? sep : rightCap);
                }
                // 如果子表比 IW+2 窄，在 rightCap 前补 ═
                int target = IW + 2;
                int current = sb2.Length;
                if (current < target)
                {
                    // 在最后一个 rightCap 前插入补充的 ═
                    sb2.Insert(sb2.Length - 1, new string('═', target - current));
                }
                return sb2.ToString();
            }

            // ══════════════════════════════════════════════════════
            // 标题区
            // ══════════════════════════════════════════════════════
            string title = string.IsNullOrEmpty(label)
                           ? $"Wall [{Id}] 世界坐标报告"
                           : $"Wall [{Id}] 世界坐标报告 - {label}";

            sb.AppendLine(OuterTop);
            sb.AppendLine(TextRow($"  {title}"));
            sb.AppendLine(TextRow($"  当前变换 : {Transform}"));
            sb.AppendLine(TextRow($"  已翻面   : {FlipCount} 次    加工原点 : {MachineOrigin}"));
            sb.AppendLine(OuterMid);

            // ══════════════════════════════════════════════════════
            // 【轮廓顶点表】
            // ══════════════════════════════════════════════════════
            sb.AppendLine(TextRow($"  【轮廓顶点】共 {Outline.Count} 个顶点，每点含底面 / 顶面坐标"));

            int[] vtxCols = { W_IDX, W_LOCAL, W_BOTTOM, W_TOP };

            sb.AppendLine(MakeSubLine('╠', '╦', '╣', vtxCols));
            sb.AppendLine(TableRow(
                $" {"#",W_IDX} ║" +
                $" {"局部坐标 (XY)",-(W_LOCAL)} ║" +
                $" {"底面世界坐标 (Z=低)",-(W_BOTTOM)} ║" +
                $" {"顶面世界坐标 (Z=高)",-(W_TOP)} "));
            sb.AppendLine(MakeSubLine('╠', '╬', '╣', vtxCols));

            for (int i = 0; i < Outline.Count; i++)
            {
                Vec2 localPt = Outline[i];
                Vec3 bottomWorld = Transform.Apply(
                    new Vec3(localPt.X, localPt.Y, BaseElevation));
                Vec3 topWorld = Transform.Apply(
                    new Vec3(localPt.X, localPt.Y, BaseElevation + Thickness));

                sb.AppendLine(TableRow(
                    $" {i.ToString(),W_IDX} ║" +
                    $" {localPt.ToString(),-(W_LOCAL)} ║" +
                    $" {bottomWorld.ToString(),-(W_BOTTOM)} ║" +
                    $" {topWorld.ToString(),-(W_TOP)} "));
            }

            sb.AppendLine(OuterMid);

            // ══════════════════════════════════════════════════════
            // 【加工特征表】
            // ══════════════════════════════════════════════════════
            sb.AppendLine(TextRow($"  【加工特征】共 {Features.Count} 个特征"));

            int[] ftCols = { W_FID, W_FTYPE, W_INITSIDE, W_CURSIDE, W_FWORLD };

            sb.AppendLine(MakeSubLine('╠', '╦', '╣', ftCols));
            sb.AppendLine(TableRow(
                $" {"ID",-(W_FID)} ║" +
                $" {"类型",-(W_FTYPE)} ║" +
                $" {"初始面",-(W_INITSIDE)} ║" +
                $" {"当前面",-(W_CURSIDE)} ║" +
                $" {"世界坐标",-(W_FWORLD)} "));
            sb.AppendLine(MakeSubLine('╠', '╬', '╣', ftCols));

            foreach (var f in Features)
            {
                Vec3 worldPos = GetFeatureWorldPos(f);
                string changed = f.InitialSide != f.CurrentSide ? "★" : " ";
                string curSide = changed + f.CurrentSide.ToString();

                sb.AppendLine(TableRow(
                    $" {f.Id,-(W_FID)} ║" +
                    $" {f.Type.ToString(),-(W_FTYPE)} ║" +
                    $" {f.InitialSide.ToString(),-(W_INITSIDE)} ║" +
                    $" {curSide,-(W_CURSIDE)} ║" +
                    $" {worldPos.ToString(),-(W_FWORLD)} "));

                // Groove 额外打印起点 / 终点
                if (f is Groove groove)
                {
                    Vec3 startWorld = Transform.Apply(
                        new Vec3(groove.StartPt.X, groove.StartPt.Y, BaseElevation));
                    Vec3 endWorld = Transform.Apply(
                        new Vec3(groove.EndPt.X, groove.EndPt.Y, BaseElevation));

                    sb.AppendLine(TableRow(
                        $" {"",-(W_FID)} ║" +
                        $" {"",-(W_FTYPE)} ║" +
                        $" {"  └─ 起点",-(W_INITSIDE)} ║" +
                        $" {"",-(W_CURSIDE)} ║" +
                        $" {"→ " + startWorld.ToString(),-(W_FWORLD)} "));

                    sb.AppendLine(TableRow(
                        $" {"",-(W_FID)} ║" +
                        $" {"",-(W_FTYPE)} ║" +
                        $" {"  └─ 终点",-(W_INITSIDE)} ║" +
                        $" {"",-(W_CURSIDE)} ║" +
                        $" {"→ " + endWorld.ToString(),-(W_FWORLD)} "));
                }
            }

            sb.AppendLine(OuterMid);

            // ══════════════════════════════════════════════════════
            // 【法向量方向表】
            // ══════════════════════════════════════════════════════
            sb.AppendLine(TextRow("  【法向量方向（旋转后）】"));

            int[] nmCols = { W_NID, W_NORMAL, W_DIRECTION };

            sb.AppendLine(MakeSubLine('╠', '╦', '╣', nmCols));
            sb.AppendLine(TableRow(
                $" {"ID",-(W_NID)} ║" +
                $" {"当前法向量",-(W_NORMAL)} ║" +
                $" {"朝向描述",-(W_DIRECTION)} "));
            sb.AppendLine(MakeSubLine('╠', '╬', '╣', nmCols));

            foreach (var f in Features)
            {
                string direction = GetDirectionDesc(f.CurrentNormal);
                sb.AppendLine(TableRow(
                    $" {f.Id,-(W_NID)} ║" +
                    $" {f.CurrentNormal.ToString(),-(W_NORMAL)} ║" +
                    $" {direction,-(W_DIRECTION)} "));
            }

            sb.AppendLine(OuterMid);

            // ══════════════════════════════════════════════════════
            // 【包围盒 AABB】
            // ══════════════════════════════════════════════════════
            var (bmin, bmax) = GetBoundingBox();
            Vec3 size = bmax - bmin;

            sb.AppendLine(TextRow("  【世界坐标包围盒 AABB】"));
            sb.AppendLine(TextRow($"  Min  : {bmin}"));
            sb.AppendLine(TextRow($"  Max  : {bmax}"));
            sb.AppendLine(TextRow($"  Size : X={size.X:F2}mm    Y={size.Y:F2}mm    Z={size.Z:F2}mm"));
            sb.AppendLine(OuterBottom);

            Console.Write(sb);
        }

        /// <summary>根据法向量返回中文朝向描述</summary>
        private static string GetDirectionDesc(Vec3 normal)
        {
            var n = normal.Normalize();
            float ax = MathF.Abs(n.X);
            float ay = MathF.Abs(n.Y);
            float az = MathF.Abs(n.Z);

            if (az >= ax && az >= ay)
                return n.Z > 0 ? "朝上   (+Z / Top)"
                               : "朝下   (-Z / Bottom)";
            if (ay >= ax)
                return n.Y > 0 ? "朝前   (+Y / Front)"
                               : "朝后   (-Y / Back)";
            return n.X > 0 ? "朝右   (+X / Right)"
                               : "朝左   (-X / Left)";
        }

        // ══════════════════════════════════════════════
        // 翻面快照（含新增字段）
        // ══════════════════════════════════════════════

        private class FlipSnapshot
        {
            private readonly List<Vec2> _outline;
            private readonly List<Vec2> _featurePositions;
            private readonly List<MachineSide> _featureSides;
            private readonly Vec2 _machineOrigin;
            private readonly int _flipCount;
            // 新增：快照实际尺寸和 Pivot
            private readonly Vec3? _pivotPoint;
            private readonly float? _actualLength;
            private readonly float? _actualWidth;
            private readonly float? _actualThickness;

            private FlipSnapshot(
                List<Vec2> outline,
                List<Vec2> featurePositions,
                List<MachineSide> featureSides,
                Vec2 machineOrigin,
                int flipCount,
                Vec3? pivotPoint,
                float? actualLength,
                float? actualWidth,
                float? actualThickness)
            {
                _outline = outline;
                _featurePositions = featurePositions;
                _featureSides = featureSides;
                _machineOrigin = machineOrigin;
                _flipCount = flipCount;
                _pivotPoint = pivotPoint;
                _actualLength = actualLength;
                _actualWidth = actualWidth;
                _actualThickness = actualThickness;
            }

            public static FlipSnapshot Capture(
                List<Vec2> outline,
                List<Feature> features,
                Vec2 machineOrigin,
                int flipCount,
                Vec3? pivotPoint,
                float? actualLength,
                float? actualWidth,
                float? actualThickness)
                => new FlipSnapshot(
                    new List<Vec2>(outline),
                    features.Select(f => f.LocalPos).ToList(),
                    features.Select(f => f.Face.GetCurrentSide()).ToList(),
                    machineOrigin,
                    flipCount,
                    pivotPoint,
                    actualLength,
                    actualWidth,
                    actualThickness);

            public void Restore(MomWall wall)
            {
                wall.Outline = new List<Vec2>(_outline);
                wall.MachineOrigin = _machineOrigin;
                wall.FlipCount = _flipCount;
                wall._pivotPoint = _pivotPoint;
                wall._actualLength = _actualLength;
                wall._actualWidth = _actualWidth;
                wall._actualThickness = _actualThickness;

                for (int i = 0;
                     i < wall.Features.Count && i < _featurePositions.Count;
                     i++)
                {
                    wall.Features[i].LocalPos = _featurePositions[i];
                    wall.Features[i].Face.ApplyFlipSide(_featureSides[i]);
                }
            }
        }
    }
}
