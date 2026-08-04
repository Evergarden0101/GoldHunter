namespace GoldHunter.Core.Math
{
    /// <summary>Scalar helpers shared across the simulation.</summary>
    public static class GhMath
    {
        public const float Tau = 6.28318530718f;
        public const float Pi = 3.14159265359f;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int ClampInt(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a == b ? 0f : (v - a) / (b - a);
        public static float Sign(float v) => v < 0f ? -1f : (v > 0f ? 1f : 0f);
        public static float Abs(float v) => v < 0f ? -v : v;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;

        /// <summary>Frame-rate independent exponential smoothing.</summary>
        public static float Damp(float current, float target, float rate, float dt)
        {
            return Lerp(current, target, 1f - (float)System.Math.Exp(-rate * dt));
        }

        /// <summary>Shortest signed delta between two angles, in radians.</summary>
        public static float AngleDelta(float from, float to)
        {
            float d = (to - from) % Tau;
            if (d > Pi) d -= Tau;
            if (d < -Pi) d += Tau;
            return d;
        }

        public static float RotateToward(float from, float to, float maxStep)
        {
            return from + Clamp(AngleDelta(from, to), -maxStep, maxStep);
        }

        public static float EaseOutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
