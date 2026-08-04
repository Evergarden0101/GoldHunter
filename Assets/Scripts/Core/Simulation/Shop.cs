using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
    /// <summary>A shop stall. Solid to walk into, with a browse ring around it.</summary>
    public sealed class Shop
    {
        private readonly ShopSettings _settings;

        public string Id { get; }
        public Vec2 Position { get; }
        public float Radius => _settings.Radius;
        public float BrowseRange => _settings.BrowseRange;

        /// <summary>Player indices currently browsing, so views can lay out panels.</summary>
        public readonly HashSet<int> Customers = new HashSet<int>();

        public Shop(string id, Vec2 position, ShopSettings settings)
        {
            Id = id;
            Position = position;
            _settings = settings;
        }

        public bool IsInBrowseRange(Vec2 point, float bodyRadius)
        {
            return Vec2.Distance(point, Position) <= _settings.BrowseRange + bodyRadius;
        }
    }
}
