namespace Infrastructure.Maths
{
    /// <summary>
    /// 二维向量（俯视图轮廓 / 特征局部坐标使用）
    /// </summary>
    public struct Vec2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public static readonly Vec2 Zero = new Vec2(0, 0);
        public Vec2() { X = 0; Y = 0; }
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

        /// <summary>长度的平方（避免开根号，用于比较大小）</summary>
        public float LengthSquared() => X * X + Y * Y;

        /// <summary>归一化</summary>
        public Vec2 Normalize()
        {
            float l = Length();
            return l < 1e-6f ? Zero : new Vec2(X / l, Y / l);
        }

        /// <summary>从角度和距离生成偏移向量（角度单位：度）</summary>
        public static Vec2 FromAngle(float angleDeg, float length)
        {
            float rad = angleDeg * MathF.PI / 180f;
            return new Vec2(
                MathF.Cos(rad) * length,
                MathF.Sin(rad) * length
            );
        }

        public override string ToString() => $"({X:F3}, {Y:F3})";
    }
}
