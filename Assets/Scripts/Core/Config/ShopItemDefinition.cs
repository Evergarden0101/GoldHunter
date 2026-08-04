using System;

namespace GoldHunter.Core.Config
{
    /// <summary>
    /// One row of the shop catalogue. Price is linear in the level already owned:
    /// <c>BasePrice + level * PriceStep</c>, so a flat-priced item just uses step 0.
    /// </summary>
    [Serializable]
    public class ShopItemDefinition
    {
        public ItemId Id = ItemId.AttackUp;
        public string DisplayName = "Attack Up";
        public string Description = "";

        /// <summary>Price of the first level.</summary>
        public float BasePrice = 28f;

        /// <summary>Added to the price for each level already owned.</summary>
        public float PriceStep = 24f;

        /// <summary>How many levels can be bought.</summary>
        public int MaxLevel = 4;

        public int PriceAtLevel(int level)
        {
            return (int)System.Math.Round(BasePrice + level * PriceStep);
        }
    }
}
