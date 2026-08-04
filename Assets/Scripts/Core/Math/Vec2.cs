using System;

namespace GoldHunter.Core.Math
{
    /// <summary>
    /// Minimal 2D vector. The core deliberately does not use UnityEngine.Vector2
    /// so the simulation stays compilable and testable outside the editor.
    /// </summary>
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);

        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => (float)System.Math.Sqrt(X * X + Y * Y);

        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? new Vec2(X / m, Y / m) : Zero;
            }
        }

        /// <summary>Angle in radians, measured the same way as Atan2(y, x).</summary>
        public float Angle => (float)System.Math.Atan2(Y, X);

        public static Vec2 FromAngle(float radians, float length = 1f)
        {
            return new Vec2((float)System.Math.Cos(radians) * length,
                            (float)System.Math.Sin(radians) * length);
        }

        public static float Distance(Vec2 a, Vec2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        public static float SqrDistance(Vec2 a, Vec2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            return dx * dx + dy * dy;
        }

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) =>
            new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }
}
