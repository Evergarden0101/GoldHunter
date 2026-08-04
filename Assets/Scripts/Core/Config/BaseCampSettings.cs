using System;

namespace GoldHunter.Core.Config
{
    /// <summary>Vault behaviour: banking speed and how much a raid takes.</summary>
    [Serializable]
    public class BaseCampSettings
    {
        public float Radius = 3.2f;

        /// <summary>Gold per second moved from bag to vault while standing in your camp.</summary>
        public float DepositRatePerSecond = 95f;

        /// <summary>Seconds before the same thief can rob the same vault again.</summary>
        public float StealCooldown = 4.5f;

        /// <summary>Share of the vault taken by one successful raid.</summary>
        public float StealFraction = 0.25f;

        /// <summary>Hard ceiling on a single raid.</summary>
        public float StealCap = 70f;

        /// <summary>A raid always takes at least this much (if the vault holds it).</summary>
        public float StealMin = 10f;
    }
}
