

namespace GoldHunter.Core.Services
{
    /// <summary>One shop line as the UI needs it: price, level, and why it is or isn't buyable.</summary>
    public struct ShopRow
    {
        public ShopItemDefinition Item;
        public int Level;
        public int Price;
        public bool IsMaxed;

        /// <summary>Affordable from bag + vault combined.</summary>
        public bool IsAffordable;

        /// <summary>The bag alone cannot cover it, so the vault has to chip in.</summary>
        public bool NeedsVault;
    }
}
