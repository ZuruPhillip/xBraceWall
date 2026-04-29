namespace Infrastructure.Maths
{
    /// <summary>
    /// 四元数（三维旋转 / SLERP 插值）
    /// </summary>
    public struct Quaternion
    {
        public float W, X, Y, Z;

        public static readonly Quaternion Identity = new Quaternion(1, 0, 0, 0);

        public Quaternion(float w, float x, float y, float z)
        {
            W = w; X = x; Y = y; Z = z;
        }

        // ── 工厂方法 ──────────────────────────────────────────

        /// <summary>从轴角构造（弧度）</summary>
        public static Quaternion FromAxisAngle(Vec3 axis, float angleRad)
        {
            Vec3 a = axis.Normalize();
            float s = MathF.Sin(angleRad * 0.5f);
            return new Quaternion(
                MathF.Cos(angleRad * 0.5f),
                a.X * s, a.Y * s, a.Z * s);
        }

        /// <summary>从欧拉角构造（ZYX顺序，弧度）</summary>
        public static Quaternion FromEuler(float roll, float pitch, float yaw)
        {
            float cr = MathF.Cos(roll * 0.5f), sr = MathF.Sin(roll * 0.5f);
            float cp = MathF.Cos(pitch * 0.5f), sp = MathF.Sin(pitch * 0.5f);
            float cy = MathF.Cos(yaw * 0.5f), sy = MathF.Sin(yaw * 0.5f);
            return new Quaternion(
                cr * cp * cy + sr * sp * sy,
                sr * cp * cy - cr * sp * sy,
                cr * sp * cy + sr * cp * sy,
                cr * cp * sy - sr * sp * cy);
        }

        // ── 运算 ──────────────────────────────────────────────

        /// <summary>四元数乘法（复合旋转）</summary>
        public static Quaternion operator *(Quaternion a, Quaternion b) =>
            new Quaternion(
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);

        /// <summary>共轭（单位四元数下等于逆）</summary>
        public Quaternion Conjugate() => new Quaternion(W, -X, -Y, -Z);

        /// <summary>模长</summary>
        public float Norm() => MathF.Sqrt(W * W + X * X + Y * Y + Z * Z);

        /// <summary>归一化</summary>
        public Quaternion Normalize()
        {
            float n = Norm();
            return n < 1e-6f ? Identity : new Quaternion(W / n, X / n, Y / n, Z / n);
        }

        /// <summary>旋转三维点：p' = q * p * q⁻¹</summary>
        public Vec3 Rotate(Vec3 v)
        {
            var p = new Quaternion(0, v.X, v.Y, v.Z);
            var r = this * p * Conjugate();
            return new Vec3(r.X, r.Y, r.Z);
        }

        /// <summary>SLERP 球面线性插值</summary>
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            float dot = a.W * b.W + a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            if (dot < 0) { b = new Quaternion(-b.W, -b.X, -b.Y, -b.Z); dot = -dot; }

            if (dot > 0.9995f)
                return new Quaternion(
                    a.W + t * (b.W - a.W), a.X + t * (b.X - a.X),
                    a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z)).Normalize();

            float theta0 = MathF.Acos(dot);
            float theta = theta0 * t;
            float s0 = MathF.Cos(theta) - dot * MathF.Sin(theta) / MathF.Sin(theta0);
            float s1 = MathF.Sin(theta) / MathF.Sin(theta0);
            return new Quaternion(
                s0 * a.W + s1 * b.W, s0 * a.X + s1 * b.X,
                s0 * a.Y + s1 * b.Y, s0 * a.Z + s1 * b.Z);
        }

        public override string ToString() =>
            $"Q(w={W:F4}, x={X:F4}, y={Y:F4}, z={Z:F4})";
    }
}
