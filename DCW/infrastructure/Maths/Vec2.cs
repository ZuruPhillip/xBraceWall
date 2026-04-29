namespace Infrastructure.Maths
{
    /// <summary>
    /// 二维向量（俯视图轮廓 / 特征局部坐标使用）
    /// </summary>
    public struct Vec2
    {
        public float X, Y;

        public static readonly Vec2 Zero = new Vec2(0, 0);

        public Vec2(float x, float y) { X = x; Y = y; }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

        /// <summary>二维叉积（返回标量）</summary>
        public float Cross(Vec2 v) => X * v.Y - Y * v.X;

        /// <summary>点积</summary>
        public float Dot(Vec2 v) => X * v.X + Y * v.Y;

        /// <summary>长度</summary>
        public float Length() => MathF.Sqrt(X * X + Y * Y);

        /// <summary>归一化</summary>
        public Vec2 Normalize()
        {
            float l = Length();
            return l < 1e-6f ? Zero : new Vec2(X / l, Y / l);
        }

        public override string ToString() => $"({X:F3}, {Y:F3})";
    }
}
