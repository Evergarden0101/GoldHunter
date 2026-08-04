using System;

namespace GoldHunter.Core.Config
{
    /// <summary>What one level of each upgrade actually does.</summary>
    [Serializable]
    public class UpgradeSettings
    {
        /// <summary>Extra gold ripped and knockback dealt, per Attack Up level.</summary>
        public float AttackPerLevel = 0.22f;

        /// <summary>Gold-loss reduction per Defense Up level (compounding).</summary>
        public float DefensePerLevel = 0.18f;

        /// <summary>Carry capacity added per Gold Bag Up level.</summary>
        public float BagPerLevel = 25f;

        /// <summary>Vault theft reduction per Base Camp Up level (compounding).</summary>
        public float CampArmorPerLevel = 0.3f;

        /// <summary>Deposit speed added per Base Camp Up level.</summary>
        public float CampDepositPerLevel = 0.35f;

        /// <summary>End-of-match vault bonus per Base Camp Up level.</summary>
        public float CampEndBonusPerLevel = 0.04f;

        /// <summary>Size delta per scale level (Scale Up positive, Scale Down negative).</summary>
        public float ScaleStep = 0.16f;

        /// <summary>Speed change per scale level. Negative: bigger is slower.</summary>
        public float ScaleSpeedPerLevel = -0.1f;

        /// <summary>Punch power change per scale level.</summary>
        public float ScalePowerPerLevel = 0.18f;

        /// <summary>Reach change per scale level.</summary>
        public float ScaleReachPerLevel = 0.22f;
    }
}
