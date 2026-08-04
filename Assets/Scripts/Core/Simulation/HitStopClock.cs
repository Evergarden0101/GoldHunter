namespace GoldHunter.Core.Simulation
{
    /// <summary>
    /// Hit-stop, implemented as a clock rather than an effect.
    ///
    /// The rule that makes impacts feel like impacts: this scales the
    /// *simulation* dt only. Presentation (particles, camera, audio) must keep
    /// running on real time, or a hit reads as a dropped frame instead of a
    /// snap.
    /// </summary>
    public sealed class HitStopClock
    {
        /// <summary>A sliver of motion during the freeze, so it never looks hung.</summary>
        private const float FrozenTimeScale = 0.035f;

        public float Remaining { get; private set; }
        public bool IsFrozen => Remaining > 0f;

        /// <summary>Requests a freeze. Longer requests win; they never stack.</summary>
        public void Request(float seconds)
        {
            if (seconds > Remaining) Remaining = seconds;
        }

        /// <summary>Converts a real frame delta into the simulation delta.</summary>
        public float ScaleDelta(float realDt)
        {
            if (Remaining > 0f)
            {
                Remaining -= realDt;
                if (Remaining < 0f) Remaining = 0f;
                return realDt * FrozenTimeScale;
            }
            return realDt;
        }

        public void Clear() => Remaining = 0f;
    }
}
