using CncWallStation.Features;
using Infrastructure.Maths;

/// <summary>
/// 圆形/腰形开孔特征
/// </summary>
public class Hole : Feature
{
    // ── 通用属性 ──────────────────────────────────────────────

    /// <summary>孔形状（圆孔 / 腰孔）</summary>
    public HoleShape Shape { get; set; } = HoleShape.Round;

    /// <summary>孔半径（mm）— 圆孔直径的一半；腰孔两端半圆半径</summary>
    public float Radius { get; set; }

    /// <summary>是否通孔</summary>
    public bool ThroughHole { get; set; }

    /// <summary>孔直径（圆孔适用）</summary>
    public float Diameter => Radius * 2f;

    // ── 腰孔专属属性 ──────────────────────────────────────────

    /// <summary>
    /// 腰孔两中心点之间的距离（mm）
    /// 圆孔时为 0
    /// </summary>
    public float SlotLength { get; set; } = 0f;

    /// <summary>
    /// 腰孔方向角（度）— 0° 表示沿 X 轴方向
    /// 圆孔时忽略
    /// </summary>
    public float SlotAngleDeg { get; set; } = 0f;

    /// <summary>
    /// 腰孔起始圆心（LocalPos 偏移后）
    /// </summary>
    public Vec2 SlotStartCenter =>
        Shape == HoleShape.Slotted
            ? LocalPos + Vec2.FromAngle(SlotAngleDeg + 180f, SlotLength / 2f)
            : LocalPos;

    /// <summary>
    /// 腰孔结束圆心（LocalPos 偏移后）
    /// </summary>
    public Vec2 SlotEndCenter =>
        Shape == HoleShape.Slotted
            ? LocalPos + Vec2.FromAngle(SlotAngleDeg, SlotLength / 2f)
            : LocalPos;

    /// <summary>腰孔整体长度（含两端半圆）</summary>
    public float TotalLength => SlotLength + Radius * 2f;

    // ── 构造函数：圆形孔 ──────────────────────────────────────

    public Hole(string id, MachineSide side,
                Vec2 center, float radius, float depth,
                bool throughHole = false)
        : base(id, FeatureType.Hole, side, center, depth)
    {
        Shape = HoleShape.Round;
        Radius = radius;
        ThroughHole = throughHole;
    }

    // ── 构造函数：腰孔 ────────────────────────────────────────

    public Hole(string id, MachineSide side,
                Vec2 center, float radius, float depth,
                float slotLength, float slotAngleDeg = 0f,
                bool throughHole = false)
        : base(id, FeatureType.Hole, side, center, depth)
    {
        Shape = HoleShape.Slotted;
        Radius = radius;
        SlotLength = slotLength;
        SlotAngleDeg = slotAngleDeg;
        ThroughHole = throughHole;
    }

    // ── 静态工厂方法（更语义化）──────────────────────────────

    /// <summary>创建圆形孔</summary>
    public static Hole CreateRound(
        string id, MachineSide side,
        Vec2 center, float radius, float depth,
        bool throughHole = false)
        => new Hole(id, side, center, radius, depth, throughHole);

    /// <summary>创建腰孔</summary>
    public static Hole CreateSlotted(
        string id, MachineSide side,
        Vec2 center, float radius, float depth,
        float slotLength, float slotAngleDeg = 0f,
        bool throughHole = false)
        => new Hole(id, side, center, radius, depth,
                    slotLength, slotAngleDeg, throughHole);

    // ── Info ─────────────────────────────────────────────────

    public override string GetInfo() => Shape switch
    {
        HoleShape.Round =>
            $"[Hole   {Id}] Side={CurrentSide,-6} " +
            $"Center={LocalPos} R={Radius}mm D={Depth}mm " +
            $"{(ThroughHole ? "[通孔]" : "[盲孔]")}",

        HoleShape.Slotted =>
            $"[Slot   {Id}] Side={CurrentSide,-6} " +
            $"Center={LocalPos} R={Radius}mm " +
            $"SlotLen={SlotLength}mm Angle={SlotAngleDeg}° " +
            $"TotalLen={TotalLength}mm D={Depth}mm " +
            $"{(ThroughHole ? "[通孔]" : "[盲孔]")}",

        _ => throw new NotSupportedException($"未处理的 HoleShape: {Shape}")
    };
}