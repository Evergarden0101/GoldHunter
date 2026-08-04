using System;

namespace GoldHunter.Core.Config
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
}
