using GoldHunter.Core.Config;

namespace GoldHunter.Core.Ai
{
    /// <summary>What a bot intends to buy on its next shop run.</summary>
    public struct ShopPlan
    {
        public bool HasPlan;

        /// <summary>The item it will actually buy right now.</summary>
        public ItemId Item;

        /// <summary>The item it most wants overall, affordable or not.</summary>
        public ItemId Dream;

        /// <summary>True when the purchase is the bot's genuine first choice.</summary>
        public bool IsDream;
    }
}
