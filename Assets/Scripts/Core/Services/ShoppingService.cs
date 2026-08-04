using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Services
{
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
