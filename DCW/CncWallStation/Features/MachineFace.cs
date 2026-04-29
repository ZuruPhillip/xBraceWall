using Infrastructure.Maths;

namespace CncWallStation.Features
{
    /// <summary>
    /// 加工面（含法向量）
    /// 旋转 / 翻面后自动更新，始终反映当前真实朝向
    /// </summary>
    public class MachineFace
    {
        // ── 标准面法向量表 ────────────────────────────────────
        private static readonly Dictionary<MachineSide, Vec3> SideNormals
            = new Dictionary<MachineSide, Vec3>
            {
                { MachineSide.Top,    new Vec3( 0,  0,  1) },
                { MachineSide.Bottom, new Vec3( 0,  0, -1) },
                { MachineSide.Front,  new Vec3( 0,  1,  0) },
                { MachineSide.Back,   new Vec3( 0, -1,  0) },
                { MachineSide.Right,  new Vec3( 1,  0,  0) },
                { MachineSide.Left,   new Vec3(-1,  0,  0) },
            };

        // ── 属性 ─────────────────────────────────────────────

        /// <summary>初始加工面（定义时指定）</summary>
        public MachineSide InitialSide { get; }

        /// <summary>初始法向量</summary>
        public Vec3 InitialNormal { get; }

        /// <summary>当前法向量（旋转/翻面后实时更新）</summary>
        public Vec3 CurrentNormal { get; private set; }

        /// <summary>翻面后直接指定的面（优先于法向量推算）</summary>
        private MachineSide? _overrideSide;

        // ── 构造 ─────────────────────────────────────────────

        public MachineFace(MachineSide side)
        {
            InitialSide = side;
            InitialNormal = SideNormals.TryGetValue(side, out var n) ? n : Vec3.UnitZ;
            CurrentNormal = InitialNormal;
            _overrideSide = null;
        }

        public MachineFace(Vec3 customNormal)
        {
            InitialSide = MachineSide.Custom;
            InitialNormal = customNormal.Normalize();
            CurrentNormal = InitialNormal;
            _overrideSide = null;
        }

        // ── 更新方法（由 Feature / Wall 调用）───────────────

        /// <summary>应用四元数旋转，更新当前法向量</summary>
        internal void ApplyRotation(Quaternion rotation)
        {
            CurrentNormal = rotation.Rotate(CurrentNormal).Normalize();
            _overrideSide = null;   // 旋转后重新由法向量推算
        }

        /// <summary>翻面后直接设置新的标准加工面</summary>
        internal void ApplyFlipSide(MachineSide newSide)
        {
            _overrideSide = newSide;
            CurrentNormal = SideNormals.TryGetValue(newSide, out var n)
                            ? n : CurrentNormal;
        }

        /// <summary>重置到初始状态</summary>
        internal void Reset()
        {
            CurrentNormal = InitialNormal;
            _overrideSide = null;
        }

        // ── 查询方法 ─────────────────────────────────────────

        /// <summary>
        /// 获取当前最接近的标准加工面
        /// 翻面后优先返回直接指定的面，旋转后从法向量推算
        /// </summary>
        public MachineSide GetCurrentSide()
        {
            if (_overrideSide.HasValue) return _overrideSide.Value;

            MachineSide best = MachineSide.Custom;
            float bestDot = float.MinValue;
            foreach (var kv in SideNormals)
            {
                float dot = CurrentNormal.Dot(kv.Value);
                if (dot > bestDot) { bestDot = dot; best = kv.Key; }
            }
            return best;
        }

        /// <summary>是否与指定面方向对齐</summary>
        public bool IsFacing(MachineSide side, float tolerance = 0.999f)
        {
            return SideNormals.TryGetValue(side, out var n)
                   && CurrentNormal.Dot(n) >= tolerance;
        }

        /// <summary>是否朝上</summary>
        public bool IsFacingUp => IsFacing(MachineSide.Top);

        /// <summary>是否朝下</summary>
        public bool IsFacingDown => IsFacing(MachineSide.Bottom);

        /// <summary>是否可从上方加工（法向量Z分量 > 0.1）</summary>
        public bool IsAccessible => CurrentNormal.Z > 0.1f;

        /// <summary>获取标准面法向量（静态工具）</summary>
        public static Vec3 GetNormal(MachineSide side) =>
            SideNormals.TryGetValue(side, out var n) ? n : Vec3.Zero;

        public override string ToString() =>
            $"初始={InitialSide} | 当前法向量={CurrentNormal} | 当前面={GetCurrentSide()}";
    }
}
