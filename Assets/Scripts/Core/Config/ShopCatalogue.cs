using System;
using System.Collections.Generic;

namespace GoldHunter.Core.Config
{
    /// <summary>
    /// The full shop shelf. Kept as data so pricing can be retuned in the
    /// Inspector; nothing in the simulation hard-codes an item price.
    /// </summary>
    [Serializable]
    public class ShopCatalogue
    {
        public List<ShopItemDefinition> Items = new List<ShopItemDefinition>();

        public ShopItemDefinition Find(ItemId id)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Id == id) return Items[i];
            }
            return null;
        }

        public int IndexOf(ItemId id)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Id == id) return i;
            }
            return -1;
        }

        public int Count => Items.Count;

        /// <summary>
        /// Default catalogue. Tier-one upgrades sit under the 40g starting bag so a
        /// first purchase is always possible; Steal is the expensive prize.
        /// </summary>
        public static ShopCatalogue Default()
        {
            return new ShopCatalogue
            {
                Items = new List<ShopItemDefinition>
                {
                    new ShopItemDefinition
                    {
                        Id = ItemId.AttackUp, DisplayName = "Attack Up",
                        Description = "Punches rip +22% more gold and hit harder.",
                        BasePrice = 28f, PriceStep = 24f, MaxLevel = 4,
                    },
                    new ShopItemDefinition
                    {
                        Id = ItemId.DefenseUp, DisplayName = "Defense Up",
                        Description = "Lose 18% less gold per hit, shrug off knockback.",
                        BasePrice = 28f, PriceStep = 24f, MaxLevel = 4,
                    },
                    new ShopItemDefinition
                    {
                        Id = ItemId.GoldBagUp, DisplayName = "Gold Bag Up",
                        Description = "+25 carry capacity.",
                        BasePrice = 30f, PriceStep = 26f, MaxLevel = 4,
                    },
                    new ShopItemDefinition
                    {
                        Id = ItemId.BaseCampUp, DisplayName = "Base Camp Up",
                        Description = "Vault armour, faster deposits, +4% end bonus.",
                        BasePrice = 36f, PriceStep = 34f, MaxLevel = 3,
                    },
                    new ShopItemDefinition
                    {
                        Id = ItemId.ScaleUp, DisplayName = "Scale Up",
                        Description = "Bigger: more reach, more knockback, slower.",
                        BasePrice = 34f, PriceStep = 0f, MaxLevel = 3,
                    },
                    new ShopItemDefinition
                    {
                        Id = ItemId.ScaleDown, DisplayName = "Scale Down",
                        Description = "Smaller: faster, harder to hit, weaker punch.",
                        BasePrice = 34f, PriceStep = 0f, MaxLevel = 3,
                    },
                    new ShopItemDefinition
                    {
                        Id = ItemId.Steal, DisplayName = "Steal",
                        Description = "Punch enemy base camps to rob their vault.",
                        BasePrice = 52f, PriceStep = 0f, MaxLevel = 1,
                    },
                },
            };
        }
    }
}
