using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
    /// <summary>
    /// Cover. Rocks are what make navigation matter — without them the arena is
    /// an open field and A* is decoration.
    /// </summary>
    public sealed class RockObstacle
    {
        public Vec2 Position { get; }
        public float Radius { get; }

        /// <summary>Stable seed so a view can generate the same silhouette every run.</summary>
        public int Seed { get; }

        public RockObstacle(Vec2 position, float radius, int seed)
        {
            Position = position;
            Radius = radius;
            Seed = seed;
        }
    }
}
