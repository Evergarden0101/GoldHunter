

namespace GoldHunter.Core.Math
{
    /// <summary>Collision and visibility helpers used by physics and navigation.</summary>
    public static class Geometry
    {
        /// <summary>
        /// Circle-vs-circle overlap. Returns false when they do not touch;
        /// otherwise <paramref name="push"/> is the vector that separates A from B.
        /// </summary>
        public static bool CirclePush(Vec2 a, float ra, Vec2 b, float rb, out Vec2 push)
        {
            Vec2 d = a - b;
            float r = ra + rb;
            float sqr = d.SqrMagnitude;
            if (sqr >= r * r || sqr < 1e-9f)
            {
                push = Vec2.Zero;
                return false;
            }
            float len = (float)System.Math.Sqrt(sqr);
            push = (d / len) * (r - len);
            return true;
        }

        /// <summary>True when the segment a-&gt;b intersects (or lies inside) the circle.</summary>
        public static bool SegmentHitsCircle(Vec2 a, Vec2 b, Vec2 center, float radius)
        {
            Vec2 d = b - a;
            Vec2 f = a - center;
            float aa = d.SqrMagnitude;
            if (aa < 1e-9f) return f.SqrMagnitude <= radius * radius;

            float bb = 2f * Vec2.Dot(f, d);
            float cc = f.SqrMagnitude - radius * radius;
            float disc = bb * bb - 4f * aa * cc;
            if (disc < 0f) return false;

            float sq = (float)System.Math.Sqrt(disc);
            float t1 = (-bb - sq) / (2f * aa);
            float t2 = (-bb + sq) / (2f * aa);
            if (t1 >= 0f && t1 <= 1f) return true;
            if (t2 >= 0f && t2 <= 1f) return true;
            return t1 < 0f && t2 > 1f;
        }
    }
}
