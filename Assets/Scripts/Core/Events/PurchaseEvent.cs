using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>A completed shop purchase, split by where the gold came from.</summary>
    public struct PurchaseEvent
    {
        public PlayerState Buyer;
        public ShopItemDefinition Item;
        public int Price;

        /// <summary>Paid out of the carried bag.</summary>
        public float FromBag;

        /// <summary>Paid out of the vault — this comes straight off the final score.</summary>
        public float FromVault;

        public int NewLevel;
    }
}
