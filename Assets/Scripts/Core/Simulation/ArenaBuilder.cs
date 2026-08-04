using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
    /// <summary>
    /// Builds the playfield.
    ///
    /// Camps sit on the diagonals at the configured radius, and the poppers and
    /// shops sit on the axes, so the layout is symmetric: every player is the
    /// same distance from the motherlode, from one small popper and from one
    /// shop. Rocks form a chamber around the centre and gate each camp's lane,
    /// which is what makes the motherlode contested rather than open ground.
    /// </summary>
    public static class ArenaBuilder
    {
        /// <summary>Camp bearings in radians: NW, NE, SW, SE (screen +y is south).</summary>
        public static readonly float[] CampAngles =
        {
            -135f * GhMath.Pi / 180f,
            -45f * GhMath.Pi / 180f,
            135f * GhMath.Pi / 180f,
            45f * GhMath.Pi / 180f,
        };

        public static List<BaseCamp> BuildCamps(GameConfig config)
        {
            var camps = new List<BaseCamp>(4);
            for (int i = 0; i < 4; i++)
            {
                Vec2 position = Vec2.FromAngle(CampAngles[i], config.Arena.CampRadius);
                camps.Add(new BaseCamp(i, position, config.Camp));
            }
            return camps;
        }

        public static List<CoinPopper> BuildPoppers(GameConfig config)
        {
            return new List<CoinPopper>
            {
                new CoinPopper(PopperKind.Motherlode, Vec2.Zero, config.Motherlode, "MOTHERLODE"),
                new CoinPopper(PopperKind.Small, new Vec2(0f, -20f), config.SmallPopper, "NORTH"),
                new CoinPopper(PopperKind.Small, new Vec2(0f, 20f), config.SmallPopper, "SOUTH"),
            };
        }

        public static List<Shop> BuildShops(GameConfig config)
        {
            return new List<Shop>
            {
                new Shop("west", new Vec2(-20f, 0f), config.Shop),
                new Shop("east", new Vec2(20f, 0f), config.Shop),
            };
        }

        public static List<RockObstacle> BuildRocks(GameConfig config)
        {
            var rocks = new List<RockObstacle>();

            // Four pillars framing the centre chamber; the gaps face the camps.
            for (int i = 0; i < 4; i++)
            {
                float a = i * GhMath.Pi / 2f;
                rocks.Add(new RockObstacle(Vec2.FromAngle(a, 6.6f), 1.5f, 100 + i));
            }

            // Gate pairs on each camp-to-centre lane, forming a choke point.
            for (int i = 0; i < 4; i++)
            {
                float a = CampAngles[i];
                Vec2 centre = Vec2.FromAngle(a, 13.5f);
                Vec2 perpendicular = new Vec2(-(float)System.Math.Sin(a), (float)System.Math.Cos(a));
                rocks.Add(new RockObstacle(centre + perpendicular * 4.4f, 1.6f, 200 + i * 2));
                rocks.Add(new RockObstacle(centre - perpendicular * 4.4f, 1.6f, 201 + i * 2));
            }

            // Outer cover behind the shops and small poppers.
            for (int i = 0; i < 4; i++)
            {
                float a = i * GhMath.Pi / 2f;
                rocks.Add(new RockObstacle(Vec2.FromAngle(a, 28f), 2.3f, 300 + i));
            }

            return rocks;
        }

        /// <summary>Spawn point: just outside the camp, facing the centre.</summary>
        public static void SpawnFor(GameConfig config, int index, out Vec2 position, out float facing)
        {
            float a = CampAngles[index];
            Vec2 camp = Vec2.FromAngle(a, config.Arena.CampRadius);
            position = camp - Vec2.FromAngle(a, 1.6f);
            facing = a + GhMath.Pi;
        }
    }
}
