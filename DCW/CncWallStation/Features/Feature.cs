using CncWallStation.Features.Grooves;
using CncWallStation.Features.MepSlots;
using CncWallStation.Features.Props;
using CncWallStation.Transforms;
using Infrastructure.Maths;
using System.Text.Json.Serialization;

namespace CncWallStation.Features
{
    /// <summary>
    /// 加工特征基类
    /// </summary>
    // ── 在 Feature 基类上声明多态类型映射 ────────────────────────
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(Groove), typeDiscriminator: "Groove")]
    [JsonDerivedType(typeof(Hole), typeDiscriminator: "Hole")]
    [JsonDerivedType(typeof(Pocket), typeDiscriminator: "Pocket")]
    [JsonDerivedType(typeof(MepSlot), typeDiscriminator: "MepSlot")]
    [JsonDerivedType(typeof(Propping), typeDiscriminator: "Propping")]
    [JsonDerivedType(typeof(RebarSlot), typeDiscriminator: "RebarSlot")]
    [JsonDerivedType(typeof(Window), typeDiscriminator: "Window")]
    public abstract class Feature
    {
        // ── 基本属性 ──────────────────────────────────────────

        /// <summary>特征编号</summary>
        public string Id { get; set; }

        // ── 序列化时写出具体类型标识（反序列化时恢复多态）──
        [JsonPropertyName("featureType")]
        public string FeatureTypeName => Type.ToString();   // 新增：类型名称字符串

        /// <summary>特征类型</summary>
        public FeatureType Type { get; }

        /// <summary>特征在局部坐标系中的位置（俯视图）</summary>
        public Vec2 LocalPos { get; set; }

        /// <summary>加工深度（mm）</summary>
        public float Depth { get; set; }

        /// <summary>加工面（含法向量，旋转/翻面后自动更新）</summary>
        [JsonIgnore]
        public MachineFace Face { get; }

        /// <summary>备注</summary>
        public string Remark { get; set; } = string.Empty;

        // ── 快捷属性 ──────────────────────────────────────────

        /// <summary>初始加工面枚举</summary>
        [JsonPropertyName("initialSide")]
        public MachineSide InitialSide => Face.InitialSide;

        /// <summary>当前加工面（旋转/翻面后变化）</summary>
        [JsonPropertyName("currentSide")]
        public MachineSide CurrentSide => Face.GetCurrentSide();

        /// <summary>当前法向量</summary>
        [JsonPropertyName("currentNormal")]
        public Vec3 CurrentNormal => Face.CurrentNormal;

        // ── 构造 ──────────────────────────────────────────────

        protected Feature(string id, FeatureType type,
                          MachineSide side, Vec2 localPos, float depth)
        {
            Id = id;
            Type = type;
            Face = new MachineFace(side);
            LocalPos = localPos;
            Depth = depth;
        }

        protected Feature(string id, FeatureType type,
                          Vec3 customNormal, Vec2 localPos, float depth)
        {
            Id = id;
            Type = type;
            Face = new MachineFace(customNormal);
            LocalPos = localPos;
            Depth = depth;
        }

        // ── 内部更新（由 Wall 统一调用）──────────────────────

        /// <summary>应用四元数旋转（更新加工面法向量）</summary>
        internal void ApplyRotation(Quaternion rotation)
            => Face.ApplyRotation(rotation);

        /// <summary>重置加工面到初始状态</summary>
        internal void ResetFace() => Face.Reset();

        /// <summary>
        /// 翻面：重映射局部坐标 + 更新加工面
        /// 子类可重写以处理额外坐标（如 Groove 的起终点）
        /// </summary>
        internal virtual void ApplyFlip(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            LocalPos = FlipRemapper.RemapPoint(LocalPos, axis, bounds);
            Face.ApplyFlipSide(
                FlipRemapper.RemapSide(Face.GetCurrentSide(), axis));
        }

        // ── 抽象方法 ──────────────────────────────────────────

        public abstract string GetInfo();

        public override string ToString() => GetInfo();
    }
}
