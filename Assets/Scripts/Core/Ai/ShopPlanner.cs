using GoldHunter.Core.Config;
using GoldHunter.Core.Math;
using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Ai
{
    /// <summary>
    /// Decides what a bot buys.
    ///
    /// The important rule is the reserve: shops can bill the vault, and the
    /// vault is the score, so a bot only dips into it while there is still time
    /// to earn the gold back. Without that guard bots convert their entire
    /// winnings into upgrades and finish near zero.
    /// </summary>
    public sealed class ShopPlanner
    {
        /// <summary>Vault gold a bot refuses to spend by the end of the match.</summary>
        private const float LateMatchReserve = 150f;

        /// <summary>Fraction of the target price at which a bot starts saving instead of spending.</summary>
        private const float SaveUpThreshold = 0.5f;

        private readonly PlayerState _player;
        private readonly NpcProfile _profile;
        private readonly ShoppingService _shopping;
        private readonly GameConfig _config;

        public ShopPlanner(PlayerState player, NpcProfile profile,
                           ShoppingService shopping, GameConfig config)
        {
            _player = player;
            _profile = profile;
            _shopping = shopping;
            _config = config;
        }

        /// <summary>Highest-bias item that is not maxed out and fits the price ceiling.</summary>
        private bool TryRankBest(int leaderIndex, float maxPrice, out ItemId best)
        {
            best = ItemId.AttackUp;
            float bestScore = 0f;
            bool found = false;

            for (int i = 0; i < _config.Catalogue.Items.Count; i++)
            {
                ShopItemDefinition def = _config.Catalogue.Items[i];
                if (_shopping.IsMaxed(_player, def.Id)) continue;
                if (_shopping.PriceOf(_player, def.Id) > maxPrice) continue;

                float weight = _profile.BiasFor(def.Id);
                if (weight <= 0.05f) continue;

                float score = weight * (1f - _player.GetLevel(def.Id) * 0.12f);
                if (def.Id == ItemId.Steal) score *= 0.6f + _profile.StealWill;
                if (def.Id == ItemId.BaseCampUp && leaderIndex == _player.Index) score *= 1.4f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = def.Id;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Builds the plan for right now.
        /// </summary>
        /// <param name="timeRemaining">Seconds left; drives the spending reserve.</param>
        /// <param name="matchDuration">Full match length, for scaling that reserve.</param>
        public ShopPlan Plan(int leaderIndex, float timeRemaining, float matchDuration)
        {
            var plan = new ShopPlan { HasPlan = false };
            if (!TryRankBest(leaderIndex, float.MaxValue, out ItemId dream)) return plan;
            plan.Dream = dream;

            float reserve = (1f - GhMath.Clamp01(timeRemaining / matchDuration)) * LateMatchReserve;
            float budget = GhMath.Max(0f, _shopping.Funds(_player) - reserve);
            int dreamPrice = _shopping.PriceOf(_player, dream);

            if (budget >= dreamPrice)
            {
                plan.HasPlan = true;
                plan.Item = dream;
                plan.IsDream = true;
                return plan;
            }

            // Within reach: wait rather than frittering the gold on the cheap
            // shelf. Saving costs nothing here because the funds sit safely in
            // the vault — without this the Thief never accumulates enough for
            // Steal and the whole vault-raiding game never happens.
            if (budget >= dreamPrice * SaveUpThreshold) return plan;

            if (TryRankBest(leaderIndex, budget, out ItemId affordable))
            {
                plan.HasPlan = true;
                plan.Item = affordable;
                plan.IsDream = affordable == dream;
            }
            return plan;
        }
    }
}
