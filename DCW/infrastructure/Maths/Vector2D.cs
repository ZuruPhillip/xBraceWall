using System.Numerics;

namespace Infrastructure.Maths
{
    /// <summary>
    /// 双精度二维向量（不可变值类型）
    /// </summary>
    public readonly struct Vector2D : IEquatable<Vector2D>
    {
        public static readonly Vector2D Zero = new(0, 0);
        public static readonly Vector2D One = new(1, 1);
        public static readonly Vector2D UnitX = new(1, 0);
        public static readonly Vector2D UnitY = new(0, 1);

        public double X { get; }
        public double Y { get; }

        // ── 构造 ────────────────────────────────────────────────
        public Vector2D(double x, double y) => (X, Y) = (x, y);
        public Vector2D(Vector2 v) : this(v.X, v.Y) { }

        // ── 属性 ────────────────────────────────────────────────

        /// <summary>向量长度（模）</summary>
        public double Length => Math.Sqrt(LengthSquared);

        /// <summary>长度平方（避免开方，性能更好）</summary>
        public double LengthSquared => X * X + Y * Y;

        /// <summary>是否为零向量</summary>
        public bool IsZero => LengthSquared == 0;

        // ── 运算方法 ─────────────────────────────────────────────

        /// <summary>归一化，返回单位向量</summary>
        /// <exception cref="InvalidOperationException">零向量无法归一化</exception>
        public Vector2D Normalize()
        {
            if (IsZero)
                throw new InvalidOperationException("零向量无法归一化。");

            double invLen = 1.0 / Length;  // 乘法比两次除法更快
            return new Vector2D(X * invLen, Y * invLen);
        }

        /// <summary>尝试归一化，零向量返回 false</summary>
        public bool TryNormalize(out Vector2D result)
        {
            if (IsZero)
            {
                result = Zero;
                return false;
            }
            result = Normalize();
            return true;
        }

        /// <summary>点积</summary>
        public double Dot(Vector2D other) => X * other.X + Y * other.Y;

        /// <summary>叉积（返回标量，表示 Z 分量）</summary>
        public double Cross(Vector2D other) => X * other.Y - Y * other.X;

        /// <summary>与另一向量的距离</summary>
        public double DistanceTo(Vector2D other) => (this - other).Length;

        /// <summary>与另一向量的夹角（弧度）</summary>
        public double AngleTo(Vector2D other)
        {
            double denominator = Length * other.Length;
            if (denominator == 0) return 0;
            double cos = Math.Clamp(Dot(other) / denominator, -1.0, 1.0);
            return Math.Acos(cos);
        }

        /// <summary>线性插值</summary>
        public Vector2D Lerp(Vector2D target, double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return new Vector2D(X + (target.X - X) * t,
                                Y + (target.Y - Y) * t);
        }

        // ── 运算符 ───────────────────────────────────────────────
        public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
        public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
        public static Vector2D operator -(Vector2D v) => new(-v.X, -v.Y);           // 取反

        /// <summary>分量乘（Hadamard积）</summary>
        public static Vector2D operator *(Vector2D a, Vector2D b) => new(a.X * b.X, a.Y * b.Y);

        /// <summary>标量缩放</summary>
        public static Vector2D operator *(Vector2D v, double s) => new(v.X * s, v.Y * s);
        public static Vector2D operator *(double s, Vector2D v) => v * s;                     // 交换律
        public static Vector2D operator /(Vector2D v, double s)
        {
            if (s == 0) throw new DivideByZeroException("向量不能除以零。");
            return new Vector2D(v.X / s, v.Y / s);
        }

        // ── 相等性 ───────────────────────────────────────────────
        public bool Equals(Vector2D other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is Vector2D v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Vector2D a, Vector2D b) => a.Equals(b);
        public static bool operator !=(Vector2D a, Vector2D b) => !a.Equals(b);

        // ── 类型转换 ─────────────────────────────────────────────
        public Vector2 ToVector2() => new((float)X, (float)Y);

        /// <summary>从 Vector2（float）隐式转换</summary>
        public static implicit operator Vector2D(Vector2 v) => new(v);

        /// <summary>转为 Vector2（float）显式转换，有精度损失</summary>
        public static explicit operator Vector2(Vector2D v) => v.ToVector2();

        // ── 调试 ─────────────────────────────────────────────────
        public override string ToString() => $"({X:F4}, {Y:F4})";
        public void Deconstruct(out double x, out double y) => (x, y) = (X, Y);
    }
}