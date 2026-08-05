

namespace GoldHunter.Core.Math
{
    /// <summary>
    /// Seeded mulberry32 PRNG. The simulation never touches UnityEngine.Random,
    /// so a match replays identically from the same seed — which is what makes
    /// the headless balance harness meaningful.
    /// </summary>
    public sealed class DeterministicRng
    {
        private uint _state;

        public DeterministicRng(int seed)
        {
            _state = unchecked((uint)seed);
        }

        public float Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint t = _state;
                t = (uint)((t ^ (t >> 15)) * (t | 1u));
                t ^= t + (uint)((t ^ (t >> 7)) * (t | 61u));
                return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296f;
            }
        }

        public float Range(float lo, float hi) => lo + Next() * (hi - lo);

        public float Angle() => Next() * GhMath.Tau;
    }
}
