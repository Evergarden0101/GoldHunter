using GoldHunter.Core.Config;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>Why a buy attempt failed, so the UI can say something useful.</summary>
    public enum PurchaseRejection
    {
        AlreadyMaxLevel = 0,
        NotEnoughGold = 1,
        NotAtShop = 2,
    }

    public struct PurchaseRejectedEvent
    {
        public PlayerState Buyer;
        public ShopItemDefinition Item;
        public PurchaseRejection Reason;

        /// <summary>Gold still needed, when the reason is NotEnoughGold.</summary>
        public float Shortfall;
    }
}
