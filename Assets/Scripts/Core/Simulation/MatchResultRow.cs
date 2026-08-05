using System.Collections.Generic;
using GoldHunter.Core.Ai;
using GoldHunter.Core.Services;

namespace GoldHunter.Core.Simulation
{
    /// <summary>One line of the final standings.</summary>
    public sealed class MatchResultRow
    {
        public int PlayerIndex;
        public string Name;
        public bool IsHuman;
        public NpcProfile Profile;

        /// <summary>1 = winner.</summary>
        public int Place;

        public float Vault;

        /// <summary>Base Camp Up end bonus.</summary>
        public float Bonus;

        /// <summary>Vault + bonus. This is what ranks players.</summary>
        public float Total;

        /// <summary>Gold still in the bag at the whistle — worth nothing.</summary>
        public float Carried;

        public PlayerStats Stats;
        public int ScaleLevel;

        /// <summary>Everything this player bought, in the order they bought it.</summary>
        public IReadOnlyList<ItemId> Purchases;
    }
}
