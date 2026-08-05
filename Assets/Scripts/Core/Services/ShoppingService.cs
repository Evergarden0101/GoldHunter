using System.Collections.Generic;
using System;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Services
{
    /// <summary>Shop footprint and the buy interaction.</summary>
    [Serializable]
    public class ShopSettings
    {
        /// <summary>Physical body radius (solid).</summary>
        public float Radius = 3f;

        /// <summary>Distance at which the shop panel opens.</summary>
        public float BrowseRange = 4.6f;

        /// <summary>How long the punch button must be held to confirm a purchase.</summary>
        public float BuyHoldSeconds = 0.45f;

        /// <summary>Debounce between selection changes.</summary>
        public float CycleCooldown = 0.12f;
    }

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

    /// <summary>Every purchasable upgrade. Order here is the order shown in the shop.</summary>
    public enum ItemId
    {
        AttackUp = 0,
        DefenseUp = 1,
        GoldBagUp = 2,
        BaseCampUp = 3,
        ScaleUp = 4,
        ScaleDown = 5,
        Steal = 6,
    }

    /// <summary>
    /// All shop rules in one place: what something costs, whether it can be
    /// bought, and where the gold comes from.
    ///
    /// Purchases bill the carried bag first and the vault for the remainder.
    /// Charging the bag alone was tried first and it warps the whole game: the
    /// bag is small, so it becomes a hard price ceiling and the expensive items
    /// can only be afforded by loitering with a fat unbanked bag — which is
    /// exactly what rivals punch out of you. Vault funding keeps every upgrade
    /// reachable and makes the price honest, because it comes off the score.
    /// </summary>
    public sealed class ShoppingService
    {
        private readonly GameConfig _config;

        public ShopCatalogue Catalogue => _config.Catalogue;

        public ShoppingService(GameConfig config)
        {
            _config = config;
        }

        /// <summary>Everything this player could spend right now.</summary>
        public float Funds(PlayerState player)
        {
            return player.Bag + (player.Home != null ? player.Home.Vault : 0f);
        }

        public int PriceOf(PlayerState player, ItemId id)
        {
            ShopItemDefinition def = _config.Catalogue.Find(id);
            return def == null ? int.MaxValue : def.PriceAtLevel(player.GetLevel(id));
        }

        public bool IsMaxed(PlayerState player, ItemId id)
        {
            ShopItemDefinition def = _config.Catalogue.Find(id);
            return def == null || player.GetLevel(id) >= def.MaxLevel;
        }

        public bool CanAfford(PlayerState player, ItemId id)
        {
            return Funds(player) >= PriceOf(player, id);
        }

        public bool CanBuy(PlayerState player, ItemId id)
        {
            return !IsMaxed(player, id) && CanAfford(player, id);
        }

        /// <summary>
        /// Charges the player and applies the upgrade.
        /// </summary>
        /// <returns>False when the purchase was rejected; nothing is charged in that case.</returns>
        public bool TryPurchase(PlayerState player, ItemId id, out int price,
                                out float fromBag, out float fromVault)
        {
            price = 0;
            fromBag = 0f;
            fromVault = 0f;
            if (!CanBuy(player, id)) return false;

            price = PriceOf(player, id);
            fromBag = GhMath.Min(player.Bag, price);
            player.Bag -= fromBag;

            fromVault = price - fromBag;
            if (fromVault > 0f && player.Home != null) player.Home.Withdraw(fromVault);

            player.Stats.Spent += price;
            player.Stats.SpentFromVault += fromVault;

            int maxScale = System.Math.Max(
                _config.Catalogue.Find(ItemId.ScaleUp)?.MaxLevel ?? 3,
                _config.Catalogue.Find(ItemId.ScaleDown)?.MaxLevel ?? 3);
            player.ApplyUpgrade(id, maxScale);
            return true;
        }

        /// <summary>Snapshot of the whole shelf for this player, for the shop panel.</summary>
        public void BuildRows(PlayerState player, List<ShopRow> into)
        {
            into.Clear();
            for (int i = 0; i < _config.Catalogue.Items.Count; i++)
            {
                ShopItemDefinition def = _config.Catalogue.Items[i];
                int level = player.GetLevel(def.Id);
                int price = def.PriceAtLevel(level);
                bool maxed = level >= def.MaxLevel;
                into.Add(new ShopRow
                {
                    Item = def,
                    Level = level,
                    Price = price,
                    IsMaxed = maxed,
                    IsAffordable = !maxed && Funds(player) >= price,
                    NeedsVault = player.Bag < price,
                });
            }
        }
    }
}
