//using System.Numerics;

//namespace Infrastructure.Maths
//{
//    /// <summary>
//    /// 数学扩展工具类
//    /// </summary>
//    public static class MathExtensions
//    {
//        #region 容差常量

//        /// <summary>float 比较容差（可根据业务调整）</summary>
//        public const float FloatTolerance = 1e-4f;   

//        /// <summary>double 比较容差</summary>
//        public const double DoubleTolerance = 1e-9; 

//        #endregion

//        #region 角度 / 弧度

//        /// <summary>角度 → 弧度</summary>
//        public static float ToRadians(this float degrees) => degrees * (MathF.PI / 180f);

//        /// <summary>弧度 → 角度</summary>
//        public static float ToDegrees(this float radians) => radians * (180f / MathF.PI);

//        /// <summary>角度 → 弧度（double）</summary>
//        public static double ToRadians(this double degrees) => degrees * (Math.PI / 180.0);

//        /// <summary>弧度 → 角度（double）</summary>
//        public static double ToDegrees(this double radians) => radians * (180.0 / Math.PI);

//        #endregion

//        #region Round 舍入

//        /// <summary>float 舍入</summary>
//        public static float Round(this float value, int digits = 4)
//            => MathF.Round(value, digits);

//        /// <summary>double 舍入</summary>
//        public static double Round(this double value, int digits = 4)
//            => Math.Round(value, digits);

//        /// <summary>Vector2 各分量舍入</summary>
//        public static Vector2 Round(this Vector2 v, int digits = 4)
//            => new(MathF.Round(v.X, digits), MathF.Round(v.Y, digits));

//        /// <summary>Vector3 各分量舍入</summary>
//        public static Vector3 Round(this Vector3 v, int digits = 4)
//            => new(MathF.Round(v.X, digits),
//                   MathF.Round(v.Y, digits),
//                   MathF.Round(v.Z, digits));

//        /// <summary>
//        /// Quaternion 各分量舍入
//        /// <para>注意：Quaternion 是值类型，修改副本后返回即可，无需修改原值</para>
//        /// </summary>
//        public static Quaternion Round(this Quaternion q, int digits = 7)
//            => new(MathF.Round(q.X, digits),
//                   MathF.Round(q.Y, digits),
//                   MathF.Round(q.Z, digits),
//                   MathF.Round(q.W, digits));

//        #endregion

//        #region CloseTo 近似相等

//        /// <summary>float 近似相等</summary>
//        public static bool CloseTo(this float a, float b, float tolerance = FloatTolerance)
//            => MathF.Abs(a - b) < tolerance;

//        /// <summary>double 近似相等</summary>
//        public static bool CloseTo(this double a, double b, double tolerance = DoubleTolerance)
//            => Math.Abs(a - b) < tolerance;

//        /// <summary>Vector2 近似相等（逐分量比较）</summary>
//        public static bool CloseTo(this Vector2 a, Vector2 b, float tolerance = FloatTolerance)
//            => a.X.CloseTo(b.X, tolerance)
//            && a.Y.CloseTo(b.Y, tolerance);

//        /// <summary>Vector3 近似相等（逐分量比较）</summary>
//        public static bool CloseTo(this Vector3 a, Vector3 b, float tolerance = FloatTolerance)
//            => a.X.CloseTo(b.X, tolerance)
//            && a.Y.CloseTo(b.Y, tolerance)
//            && a.Z.CloseTo(b.Z, tolerance);

//        /// <summary>
//        /// Quaternion 近似相等
//        /// <para>考虑 q 与 -q 表示同一旋转的情况</para>
//        /// </summary>
//        public static bool CloseTo(this Quaternion a, Quaternion b, float tolerance = FloatTolerance)
//        {
//            // q 和 -q 表示同一旋转，点积接近 1 或 -1 都视为相等
//            float dot = Math.Abs(Quaternion.Dot(a, b));
//            return (1f - dot).CloseTo(0f, tolerance);
//        }

//        #endregion

//        #region Dcmp 符号比较

//        /// <summary>
//        /// 带容差的符号判断
//        /// <para>返回 -1（负）、0（近似零）、1（正）</para>
//        /// </summary>
//        public static int Dcmp(this float x)
//        {
//            if (MathF.Abs(x) < FloatTolerance) return 0;
//            return x < 0 ? -1 : 1;
//        }

//        /// <summary>
//        /// 带容差的符号判断
//        /// <para>返回 -1（负）、0（近似零）、1（正）</para>
//        /// </summary>
//        public static int Dcmp(this double x)
//        {
//            if (Math.Abs(x) < DoubleTolerance) return 0;
//            return x < 0 ? -1 : 1;
//        }

//        #endregion

//        #region 类型转换

//        /// <summary>Vector2（float）→ Vector2D（double）</summary>
//        public static Vector2D ToVector2D(this Vector2 v) => new(v.X, v.Y);

//        #endregion
//    }
//}