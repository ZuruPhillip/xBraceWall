using Infrastructure.Maths;

namespace CncWallStation.Transforms
{
    /// <summary>
    /// 三维空间变换（平移 + 四元数旋转）
    /// 用于 CNC 加工坐标定位
    /// </summary>
    public class Transform3D
    {
        /// <summary>平移向量</summary>
        public Vec3 Translation { get; set; } = Vec3.Zero;

        /// <summary>旋转四元数</summary>
        public Quaternion Rotation { get; set; } = Quaternion.Identity;

        /// <summary>旋转中心点</summary>
        public Vec3 Pivot { get; set; } = Vec3.Zero;

        // ── 变换应用 ──────────────────────────────────────────

        /// <summary>对三维点施加完整变换（旋转 + 平移）</summary>
        public Vec3 Apply(Vec3 point)
        {
            Vec3 local = point - Pivot;
            Vec3 rotated = Rotation.Rotate(local);
            return rotated + Pivot + Translation;
        }

        /// <summary>对方向向量施加旋转（法向量用，不做平移）</summary>
        public Vec3 ApplyDirection(Vec3 dir) => Rotation.Rotate(dir);

        // ── 克隆（用于历史记录）──────────────────────────────

        public Transform3D Clone() => new Transform3D
        {
            Translation = this.Translation,
            Rotation = this.Rotation,
            Pivot = this.Pivot
        };

        /// <summary>重置为单位变换</summary>
        public void Reset()
        {
            Translation = Vec3.Zero;
            Rotation = Quaternion.Identity;
            Pivot = Vec3.Zero;
        }

        public override string ToString() =>
            $"T={Translation} | R={Rotation}";
    }
}
