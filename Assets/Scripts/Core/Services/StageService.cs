using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;
using GoldHunter.Core.Navigation;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Services
{
    /// <summary>
    /// The stage: everything about the map's shape and what is where.
    ///
    /// This is the single authority on two questions the rest of the game keeps
    /// asking — "can a body be here?" (arena bounds and solid blockers) and
    /// "what is interactable at this position?". Physics, the AI and the UI all
    /// route through it, so there is exactly one definition of the playfield.
    /// </summary>
    public sealed class StageService
    {
        private readonly GameConfig _config;
        private readonly List<Obstacle> _obstacles = new List<Obstacle>();

        public IReadOnlyList<CoinPopper> Poppers { get; }
        public IReadOnlyList<Shop> Shops { get; }
        public IReadOnlyList<BaseCamp> Camps { get; }
        public IReadOnlyList<RockObstacle> Rocks { get; }

        /// <summary>Solid circular blockers. Base camps are intentionally absent.</summary>
        public IReadOnlyList<Obstacle> Obstacles => _obstacles;

        public NavGrid NavGrid { get; }

        public StageService(GameConfig config,
                            IReadOnlyList<CoinPopper> poppers,
                            IReadOnlyList<Shop> shops,
                            IReadOnlyList<BaseCamp> camps,
                            IReadOnlyList<RockObstacle> rocks)
        {
            _config = config;
            Poppers = poppers;
            Shops = shops;
            Camps = camps;
            Rocks = rocks;

            for (int i = 0; i < rocks.Count; i++)
            {
                _obstacles.Add(new Obstacle(rocks[i].Position, rocks[i].Radius, ObstacleKind.Rock, i));
            }
            for (int i = 0; i < poppers.Count; i++)
            {
                _obstacles.Add(new Obstacle(poppers[i].Position, poppers[i].Radius, ObstacleKind.CoinPopper, i));
            }
            for (int i = 0; i < shops.Count; i++)
            {
                _obstacles.Add(new Obstacle(shops[i].Position, shops[i].Radius, ObstacleKind.Shop, i));
            }

            NavGrid = new NavGrid(config.Arena, config.Navigation, _obstacles);
        }

        /* ------------------------------------------------------------ map info */

        public float ArenaHalfExtent => _config.Arena.Half;
        public float ArenaCornerCut => _config.Arena.CornerCut;

        /// <summary>True when the point lies inside the octagonal playfield.</summary>
        public bool IsInsideArena(Vec2 point, float margin = 0f) => NavGrid.IsInsideArena(point, margin);

        /// <summary>True when a body of the given radius could stand here unobstructed.</summary>
        public bool IsWalkable(Vec2 point, float bodyRadius = 0f)
        {
            if (!IsInsideArena(point, bodyRadius)) return false;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                Obstacle o = _obstacles[i];
                float r = o.Radius + bodyRadius;
                if (Vec2.SqrDistance(point, o.Position) < r * r) return false;
            }
            return true;
        }

        public bool HasLineOfSight(Vec2 from, Vec2 to) => NavGrid.HasLineOfSight(from, to);

        /* ------------------------------------------------ interactable queries */

        /// <summary>
        /// What can be interacted with at this position, for this player.
        /// Poppers win over shops, which win over camps, matching how a player
        /// would read the situation when standing in overlapping rings.
        /// </summary>
        public InteractableHit QueryInteractable(Vec2 point, float bodyRadius, int playerIndex)
        {
            InteractableHit best = InteractableHit.None;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Poppers.Count; i++)
            {
                float d = Vec2.Distance(point, Poppers[i].Position);
                if (d <= Poppers[i].HarvestRange + bodyRadius && d < bestDistance)
                {
                    bestDistance = d;
                    best = new InteractableHit
                    {
                        Kind = InteractableKind.CoinPopper, Index = i,
                        Position = Poppers[i].Position, Distance = d,
                    };
                }
            }
            if (best.Exists) return best;

            for (int i = 0; i < Shops.Count; i++)
            {
                float d = Vec2.Distance(point, Shops[i].Position);
                if (d <= Shops[i].BrowseRange + bodyRadius && d < bestDistance)
                {
                    bestDistance = d;
                    best = new InteractableHit
                    {
                        Kind = InteractableKind.Shop, Index = i,
                        Position = Shops[i].Position, Distance = d,
                    };
                }
            }
            if (best.Exists) return best;

            for (int i = 0; i < Camps.Count; i++)
            {
                float d = Vec2.Distance(point, Camps[i].Position);
                if (d <= Camps[i].Radius + bodyRadius && d < bestDistance)
                {
                    bestDistance = d;
                    best = new InteractableHit
                    {
                        Kind = Camps[i].OwnerIndex == playerIndex
                            ? InteractableKind.OwnBaseCamp
                            : InteractableKind.EnemyBaseCamp,
                        Index = i, Position = Camps[i].Position, Distance = d,
                    };
                }
            }
            return best;
        }

        public bool CanHarvestAt(Vec2 point, float bodyRadius, int popperIndex)
        {
            CoinPopper popper = Poppers[popperIndex];
            return Vec2.Distance(point, popper.Position) <= popper.HarvestRange + bodyRadius;
        }

        /// <summary>Deposits need the body to be well inside the ring, not just grazing it.</summary>
        public bool CanDepositAt(Vec2 point, float bodyRadius, BaseCamp camp)
        {
            return Vec2.Distance(point, camp.Position) <= camp.Radius + bodyRadius * 0.5f;
        }

        public Shop FindShopInRange(Vec2 point, float bodyRadius)
        {
            for (int i = 0; i < Shops.Count; i++)
            {
                if (Shops[i].IsInBrowseRange(point, bodyRadius)) return Shops[i];
            }
            return null;
        }

        public CoinPopper NearestPopper(Vec2 point)
        {
            CoinPopper best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Poppers.Count; i++)
            {
                float d = Vec2.SqrDistance(point, Poppers[i].Position);
                if (d < bestDistance) { bestDistance = d; best = Poppers[i]; }
            }
            return best;
        }

        public Shop NearestShop(Vec2 point)
        {
            Shop best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Shops.Count; i++)
            {
                float d = Vec2.SqrDistance(point, Shops[i].Position);
                if (d < bestDistance) { bestDistance = d; best = Shops[i]; }
            }
            return best;
        }

        /* --------------------------------------------------------- containment */

        /// <summary>
        /// Pushes a body out of every solid blocker it overlaps, killing the
        /// inward part of its velocity. Returns the blocker it hit hardest, if any.
        /// </summary>
        public bool ResolveBlockers(ref Vec2 position, ref Vec2 velocity, float bodyRadius,
                                    out Obstacle hitObstacle)
        {
            bool hit = false;
            hitObstacle = default;
            float deepest = 0f;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                Obstacle o = _obstacles[i];
                if (!Geometry.CirclePush(position, bodyRadius, o.Position, o.Radius, out Vec2 push)) continue;

                position += push;
                float depth = push.Magnitude;
                Vec2 normal = push / GhMath.Max(1e-5f, depth);
                float into = Vec2.Dot(velocity, normal);
                if (into < 0f) velocity -= normal * (into * (1f + _config.Arena.WallBounce));

                if (depth > deepest)
                {
                    deepest = depth;
                    hitObstacle = o;
                    hit = true;
                }
            }
            return hit;
        }

        /// <summary>Keeps a body inside the octagon: square walls plus four chamfers.</summary>
        public void ClampToArena(ref Vec2 position, ref Vec2 velocity, float bodyRadius)
        {
            float limit = _config.Arena.Half - bodyRadius;
            float bounce = _config.Arena.WallBounce;

            if (position.X < -limit) { position.X = -limit; velocity.X = GhMath.Abs(velocity.X) * bounce; }
            if (position.X > limit) { position.X = limit; velocity.X = -GhMath.Abs(velocity.X) * bounce; }
            if (position.Y < -limit) { position.Y = -limit; velocity.Y = GhMath.Abs(velocity.Y) * bounce; }
            if (position.Y > limit) { position.Y = limit; velocity.Y = -GhMath.Abs(velocity.Y) * bounce; }

            // Chamfered corners: |x| + |y| <= D. Moving along (sign x, sign y) by t
            // changes that sum by 2t, so the correction is half the overshoot.
            const float diagonal = 0.70710678f;
            float d = _config.Arena.Half * 2f - _config.Arena.CornerCut - bodyRadius * 1.41421356f;
            float sum = GhMath.Abs(position.X) + GhMath.Abs(position.Y);
            if (sum > d)
            {
                float over = (sum - d) * 0.5f;
                float sx = GhMath.Sign(position.X);
                float sy = GhMath.Sign(position.Y);
                if (sx == 0f) sx = 1f;
                if (sy == 0f) sy = 1f;
                position.X -= sx * over;
                position.Y -= sy * over;

                float into = velocity.X * sx * diagonal + velocity.Y * sy * diagonal;
                if (into > 0f)
                {
                    velocity.X -= into * sx * diagonal * (1f + bounce);
                    velocity.Y -= into * sy * diagonal * (1f + bounce);
                }
            }
        }
    }
}
