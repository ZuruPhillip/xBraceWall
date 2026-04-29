using CncWallStation.Features;
using CncWallStation.Transforms;
using Infrastructure.Maths;
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
    public class Wall
    {
        // ══════════════════════════════════════════════
        // 基本属性
        // ══════════════════════════════════════════════

        /// <summary>墙体编号</summary>
        public string Id { get; set; }

        /// <summary>材料名称</summary>
        public string Material { get; set; }

        /// <summary>备注</summary>
        public string Remark { get; set; } = string.Empty;

        // ══════════════════════════════════════════════
        // 几何定义
        // ══════════════════════════════════════════════

        /// <summary>俯视图轮廓顶点列表（逆时针顺序，局部坐标）</summary>
        public List<Vec2> Outline { get; private set; }

        /// <summary>墙体厚度 Z 方向（mm）</summary>
        public float Thickness { get; set; }

        /// <summary>底面高度（mm，默认 0）</summary>
        public float BaseElevation { get; set; }

        // ══════════════════════════════════════════════
        // 加工特征
        // ══════════════════════════════════════════════

        /// <summary>加工特征集合</summary>
        public List<Feature> Features { get; private set; } = new List<Feature>();

        // ══════════════════════════════════════════════
        // 空间变换（旋转 / 平移）
        // ══════════════════════════════════════════════

        /// <summary>当前空间变换</summary>
        public Transform3D Transform { get; private set; } = new Transform3D();

        /// <summary>变换历史栈（支持撤销）</summary>
        private readonly Stack<Transform3D> _transformHistory = new Stack<Transform3D>();

        // ══════════════════════════════════════════════
        // 翻面状态
        // ══════════════════════════════════════════════

        /// <summary>当前加工原点（局部坐标，始终为左下角）</summary>
        public Vec2 MachineOrigin { get; private set; } = Vec2.Zero;

        /// <summary>已翻面次数</summary>
        public int FlipCount { get; private set; } = 0;

        /// <summary>翻面历史快照（支持撤销翻面）</summary>
        private readonly Stack<FlipSnapshot> _flipHistory = new Stack<FlipSnapshot>();

        // ══════════════════════════════════════════════
        // 包围盒缓存
        // ══════════════════════════════════════════════

        private bool _bboxDirty = true;
        private Vec3 _bboxMin, _bboxMax;

        // ══════════════════════════════════════════════
        // 构造函数
        // ══════════════════════════════════════════════

        public Wall(string id,
                    IEnumerable<Vec2> outline,
                    float thickness,
                    float baseElevation = 0f,
                    string material = "木材")
        {
            Id = id;
            Material = material;
            Outline = new List<Vec2>(outline);
            Thickness = thickness;
            BaseElevation = baseElevation;
        }

        // ══════════════════════════════════════════════
        // 几何属性
        // ══════════════════════════════════════════════

        /// <summary>俯视图轮廓面积（Shoelace 公式）</summary>
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

        /// <summary>体积（不含特征减除量）</summary>
        public float Volume() => OutlineArea() * Thickness;

        /// <summary>
        /// 获取轮廓包围盒（局部坐标）
        /// </summary>
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

        /// <summary>点是否在轮廓内（射线法）</summary>
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
        // 特征管理
        // ══════════════════════════════════════════════

        /// <summary>添加切槽（支持链式调用）</summary>
        public Wall AddGroove(string id, MachineSide side,
                              Vec2 startPt, Vec2 endPt,
                              float width, float depth)
        {
            Features.Add(new Groove(id, side, startPt, endPt, width, depth));
            return this;
        }

        /// <summary>添加圆孔（支持链式调用）</summary>
        public Wall AddHole(string id, MachineSide side,
                            Vec2 center, float radius, float depth,
                            bool throughHole = false)
        {
            Features.Add(new Hole(id, side, center, radius, depth, throughHole));
            return this;
        }

        /// <summary>添加矩形挖坑（支持链式调用）</summary>
        public Wall AddPocket(string id, MachineSide side,
                              Vec2 center, float width, float height,
                              float depth, float cornerRadius = 0f)
        {
            Features.Add(new Pocket(id, side, center, width, height,
                                    depth, cornerRadius));
            return this;
        }

        /// <summary>按 ID 移除特征</summary>
        public bool RemoveFeature(string id)
        {
            var f = Features.FirstOrDefault(x => x.Id == id);
            return f != null && Features.Remove(f);
        }

        /// <summary>按特征类型查询</summary>
        public IEnumerable<Feature> GetFeaturesByType(FeatureType type) =>
            Features.Where(f => f.Type == type);

        /// <summary>按初始加工面查询</summary>
        public IEnumerable<Feature> GetFeaturesByInitialSide(MachineSide side) =>
            Features.Where(f => f.InitialSide == side);

        /// <summary>按当前加工面查询（旋转/翻面后使用）</summary>
        public IEnumerable<Feature> GetFeaturesByCurrentSide(MachineSide side) =>
            Features.Where(f => f.CurrentSide == side);

        /// <summary>获取当前法向量Z > 0 可从上方加工的特征</summary>
        public IEnumerable<Feature> GetAccessibleFeatures() =>
            Features.Where(f => f.Face.IsAccessible);

        // ══════════════════════════════════════════════
        // 空间变换（旋转 / 平移）
        // ══════════════════════════════════════════════

        /// <summary>平移（支持链式调用）</summary>
        public Wall Translate(Vec3 delta)
        {
            _transformHistory.Push(Transform.Clone());
            Transform.Translation = Transform.Translation + delta;
            _bboxDirty = true;
            return this;
        }

        /// <summary>
        /// 旋转（支持链式调用）
        /// 自动同步更新所有特征的加工面法向量
        /// </summary>
        /// <param name="axis">旋转轴（世界坐标系）</param>
        /// <param name="angleDeg">旋转角度（度）</param>
        /// <param name="pivot">旋转中心（null = 原点）</param>
        public Wall Rotate(Vec3 axis, float angleDeg, Vec3? pivot = null)
        {
            _transformHistory.Push(Transform.Clone());

            float rad = angleDeg * MathF.PI / 180f;
            Quaternion q = Quaternion.FromAxisAngle(axis, rad);

            Transform.Pivot = pivot ?? Vec3.Zero;
            Transform.Rotation = (q * Transform.Rotation).Normalize();

            // 同步更新所有特征的加工面法向量
            foreach (var f in Features)
                f.ApplyRotation(q);

            _bboxDirty = true;
            return this;
        }

        /// <summary>撤销上一步旋转/平移</summary>
        public bool UndoTransform()
        {
            if (_transformHistory.Count == 0) return false;
            Transform = _transformHistory.Pop();
            _bboxDirty = true;
            RebuildFeatureNormals();
            return true;
        }

        /// <summary>重置所有旋转/平移变换</summary>
        public Wall ResetTransform()
        {
            _transformHistory.Clear();
            Transform = new Transform3D();
            foreach (var f in Features) f.ResetFace();
            _bboxDirty = true;
            return this;
        }

        /// <summary>可撤销旋转/平移步数</summary>
        public int UndoTransformSteps => _transformHistory.Count;

        /// <summary>撤销后从当前旋转重建所有特征法向量</summary>
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

        /// <summary>
        /// 翻面（Top↔Bottom，坐标自动重映射，加工原点始终为左下角）
        /// 支持链式调用
        /// </summary>
        public Wall Flip(FlipAxis axis = FlipAxis.AroundX)
        {
            var bounds = GetOutlineBounds();

            // 保存翻面前完整快照
            _flipHistory.Push(FlipSnapshot.Capture(
                Outline, Features, MachineOrigin, FlipCount));

            // 重映射轮廓顶点
            for (int i = 0; i < Outline.Count; i++)
                Outline[i] = FlipRemapper.RemapPoint(Outline[i], axis, bounds);

            // 重映射所有特征坐标 + 更新加工面
            foreach (var f in Features)
                f.ApplyFlip(axis, bounds);

            // 重新计算加工原点（翻面后左下角）
            var nb = GetOutlineBounds();
            MachineOrigin = new Vec2(nb.minX, nb.minY);
            FlipCount++;
            _bboxDirty = true;
            return this;
        }

        /// <summary>撤销翻面</summary>
        public bool UndoFlip()
        {
            if (_flipHistory.Count == 0) return false;
            _flipHistory.Pop().Restore(this);
            _bboxDirty = true;
            return true;
        }

        /// <summary>可撤销翻面步数</summary>
        public int UndoFlipSteps => _flipHistory.Count;

        // ══════════════════════════════════════════════
        // 世界坐标计算
        // ══════════════════════════════════════════════

        /// <summary>
        /// 获取变换后的世界坐标顶点列表
        /// 每个轮廓点 → 底面顶点 + 顶面顶点
        /// </summary>
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

        /// <summary>获取指定特征的世界坐标</summary>
        public Vec3 GetFeatureWorldPos(Feature feature) =>
            Transform.Apply(new Vec3(
                feature.LocalPos.X,
                feature.LocalPos.Y,
                BaseElevation));

        /// <summary>所有特征的世界坐标（CNC 路径生成用）</summary>
        public IEnumerable<(Feature Feature, Vec3 WorldPos)> GetFeaturesWorldPos() =>
            Features.Select(f => (f, GetFeatureWorldPos(f)));

        // ══════════════════════════════════════════════
        // 包围盒 AABB
        // ══════════════════════════════════════════════

        /// <summary>获取轴对齐包围盒（含变换后坐标）</summary>
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
                _bboxMin = new Vec3(
                    MathF.Min(_bboxMin.X, v.X),
                    MathF.Min(_bboxMin.Y, v.Y),
                    MathF.Min(_bboxMin.Z, v.Z));
                _bboxMax = new Vec3(
                    MathF.Max(_bboxMax.X, v.X),
                    MathF.Max(_bboxMax.Y, v.Y),
                    MathF.Max(_bboxMax.Z, v.Z));
            }
            _bboxDirty = false;
        }

        // ══════════════════════════════════════════════
        // 打印输出
        // ══════════════════════════════════════════════

        /// <summary>打印墙体完整信息</summary>
        public void Print()
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════╗");
            sb.AppendLine($"║  Wall [{Id}]  材料: {Material}");
            sb.AppendLine($"║  轮廓顶点数 : {Outline.Count}");
            sb.AppendLine($"║  厚度       : {Thickness}mm   底面高度: {BaseElevation}mm");
            sb.AppendLine($"║  面积       : {OutlineArea():F2}mm²   体积: {Volume():F2}mm³");
            sb.AppendLine($"║  加工原点   : {MachineOrigin}   已翻面: {FlipCount} 次");
            sb.AppendLine($"║  当前变换   : {Transform}");
            sb.AppendLine($"║  可撤销变换 : {UndoTransformSteps} 步   可撤销翻面: {UndoFlipSteps} 步");
            sb.AppendLine($"╠══════════════════════════════════════════════╣");
            sb.AppendLine($"║  特征列表（共 {Features.Count} 个）");
            foreach (var f in Features)
                sb.AppendLine($"║    {f.GetInfo()}");

            var (bmin, bmax) = GetBoundingBox();
            sb.AppendLine($"╠══════════════════════════════════════════════╣");
            sb.AppendLine($"║  包围盒 Min : {bmin}");
            sb.AppendLine($"║  包围盒 Max : {bmax}");
            sb.AppendLine("╚══════════════════════════════════════════════╝");
            Console.Write(sb);
        }

        /// <summary>打印加工面变化报告</summary>
        public void PrintFaceReport()
        {
            Console.WriteLine("╔══════╦══════════╦══════════╦══════════════╗");
            Console.WriteLine("║ ID   ║ 初始加工面║ 当前加工面║ 当前坐标     ║");
            Console.WriteLine("╠══════╬══════════╬══════════╬══════════════╣");
            foreach (var f in Features)
            {
                string changed = f.InitialSide != f.CurrentSide ? " ★" : "  ";
                Console.WriteLine(
                    $"║ {f.Id,-4} ║ {f.InitialSide,-8} ║ " +
                    $"{f.CurrentSide,-6}{changed} ║ {f.LocalPos,-12} ║");
            }
            Console.WriteLine("╚══════╩══════════╩══════════╩══════════════╝");
        }

        // ══════════════════════════════════════════════
        // 内部：翻面快照
        // ══════════════════════════════════════════════

        /// <summary>翻面前状态快照（用于撤销）</summary>
        private class FlipSnapshot
        {
            private readonly List<Vec2> _outline;
            private readonly List<Vec2> _featurePositions;
            private readonly List<MachineSide> _featureSides;
            private readonly Vec2 _machineOrigin;
            private readonly int _flipCount;

            private FlipSnapshot(
                List<Vec2> outline,
                List<Vec2> featurePositions,
                List<MachineSide> featureSides,
                Vec2 machineOrigin,
                int flipCount)
            {
                _outline = outline;
                _featurePositions = featurePositions;
                _featureSides = featureSides;
                _machineOrigin = machineOrigin;
                _flipCount = flipCount;
            }

            public static FlipSnapshot Capture(
                List<Vec2> outline,
                List<Feature> features,
                Vec2 machineOrigin,
                int flipCount)
            {
                return new FlipSnapshot(
                    new List<Vec2>(outline),
                    features.Select(f => f.LocalPos).ToList(),
                    features.Select(f => f.Face.GetCurrentSide()).ToList(),
                    machineOrigin,
                    flipCount);
            }

            public void Restore(Wall wall)
            {
                wall.Outline = new List<Vec2>(_outline);
                wall.MachineOrigin = _machineOrigin;
                wall.FlipCount = _flipCount;

                for (int i = 0; i < wall.Features.Count && i < _featurePositions.Count; i++)
                {
                    wall.Features[i].LocalPos = _featurePositions[i];
                    wall.Features[i].Face.ApplyFlipSide(_featureSides[i]);
                }
            }
        }
    }
}
