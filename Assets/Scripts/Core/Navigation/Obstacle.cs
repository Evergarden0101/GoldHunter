using GoldHunter.Core.Math;

namespace GoldHunter.Core.Navigation
{
    /// <summary>What kind of thing a solid blocker is, so callers can react to a collision.</summary>
    public enum ObstacleKind
    {
        Rock = 0,
        CoinPopper = 1,
        Shop = 2,
    }

    /// <summary>
    /// A static circular blocker. Base camps are deliberately absent: they must
    /// stay walkable or nobody could ever deposit.
    /// </summary>
    public struct Obstacle
    {
        public Vec2 Position;
        public float Radius;
        public ObstacleKind Kind;

        /// <summary>Index into the owning collection (poppers, shops, rocks).</summary>
        public int SourceIndex;

        public Obstacle(Vec2 position, float radius, ObstacleKind kind, int sourceIndex)
        {
            Position = position;
            Radius = radius;
            Kind = kind;
            SourceIndex = sourceIndex;
        }
    }
}
