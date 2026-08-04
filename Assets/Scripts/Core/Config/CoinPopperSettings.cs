using System;

namespace GoldHunter.Core.Config
{
    /// <summary>
    /// Per-kind coin popper tuning. Every field here is surfaced in the Unity
    /// Inspector, so popping speed can be retuned without touching code.
    /// </summary>
    [Serializable]
    public class CoinPopperSettings
    {
        /// <summary>Gold present the moment the match starts.</summary>
        public float StartingGold = 50f;

        /// <summary>Generation rate. This is the "coin popping speed".</summary>
        public float GoldPerMinute = 200f;

        /// <summary>Maximum gold the machine will hold.</summary>
        public float Capacity = 320f;

        /// <summary>Gold per second siphoned into a bag by a player standing in range.</summary>
        public float HarvestRatePerSecond = 34f;

        /// <summary>Physical body radius (solid).</summary>
        public float Radius = 2.5f;

        /// <summary>How far from the centre a player can stand and still mine.</summary>
        public float HarvestRange = 3.9f;

        /// <summary>Seconds between visible "pop" jolts while generating.</summary>
        public float PopInterval = 0.34f;

        public float GoldPerSecond => GoldPerMinute / 60f;

        public static CoinPopperSettings Motherlode() => new CoinPopperSettings
        {
            StartingGold = 50f,
            GoldPerMinute = 200f,
            Capacity = 320f,
            HarvestRatePerSecond = 34f,
            Radius = 2.5f,
            HarvestRange = 3.9f,
            PopInterval = 0.34f,
        };

        public static CoinPopperSettings Small() => new CoinPopperSettings
        {
            StartingGold = 20f,
            GoldPerMinute = 80f,
            Capacity = 160f,
            HarvestRatePerSecond = 26f,
            Radius = 1.7f,
            HarvestRange = 3.1f,
            PopInterval = 0.6f,
        };
    }
}
