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
        /// <param name="angleDeg">逆时针旋转角度（度）</param>
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
