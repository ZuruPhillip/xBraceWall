namespace Infrastructure.Maths
{
    /// <summary>
    /// 三维向量（世界坐标 / 法向量使用）
    /// </summary>
    public struct Vec3
    {
        public float X, Y, Z;

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);
        public static readonly Vec3 UnitX = new Vec3(1, 0, 0);
        public static readonly Vec3 UnitY = new Vec3(0, 1, 0);
        public static readonly Vec3 UnitZ = new Vec3(0, 0, 1);

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);

        /// <summary>点积</summary>
        public float Dot(Vec3 v) => X * v.X + Y * v.Y + Z * v.Z;

        /// <summary>叉积</summary>
        public Vec3 Cross(Vec3 v) => new Vec3(
            Y * v.Z - Z * v.Y,
            Z * v.X - X * v.Z,
            X * v.Y - Y * v.X);

        /// <summary>长度</summary>
        public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>归一化</summary>
        public Vec3 Normalize()
        {
            float l = Length();
            return l < 1e-6f ? Zero : new Vec3(X / l, Y / l, Z / l);
        }

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }
}
