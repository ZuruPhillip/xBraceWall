using Infrastructure.Maths;
using System.Text.Json.Serialization;

namespace CncWallStation.Features.MepSlots
{
    /// <summary>
    /// 线槽路径段接口
    /// 每段拥有独立深度，宽度由 MepSlot 统一定义
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$segmentType")]
    [JsonDerivedType(typeof(LineSegment), typeDiscriminator: "Line")]
    [JsonDerivedType(typeof(ArcSegment), typeDiscriminator: "Arc")]
    public interface ISegment
    {
        /// <summary>段类型</summary>
        SegmentType Type { get; }

        /// <summary>段起点（局部坐标）</summary>
        Vec2 StartPoint { get; }

        /// <summary>段终点（局部坐标）</summary>
        Vec2 EndPoint { get; }

        /// <summary>本段加工深度（mm）</summary>
        float Depth { get; set; }

        /// <summary>本段近似长度（mm）</summary>
        float Length { get; }

        /// <summary>
        /// 局部宽度覆盖（null = 使用 MepSlot.Width 全槽默认值）
        /// </summary>
        float? OverrideWidth { get; set; }

        /// <summary>翻面时重映射坐标</summary>
        ISegment Remap(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds);

        /// <summary>均匀离散为折线点（用于碰撞检测 / 可视化）</summary>
        Vec2[] Tessellate(int segments = 16);

        /// <summary>简要描述</summary>
        string GetInfo();
    }

    /// <summary>段类型枚举</summary>
    public enum SegmentType
    {
        Line,   // 直线段
        Arc     // 圆弧段
    }
}
