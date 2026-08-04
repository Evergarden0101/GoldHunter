using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Navigation
{
    /// <summary>
    /// Rasterises the arena's static blockers into a walkability grid and answers
    /// line-of-sight queries.
    ///
    /// Blockers are inflated by the agent clearance before rasterising, so a path
    /// through a free cell is a path a body of that radius can actually walk.
    /// </summary>
    public sealed class NavGrid
    {
        private readonly ArenaSettings _arena;
        private readonly NavigationSettings _nav;
        private readonly List<Obstacle> _obstacles;
        private readonly bool[] _blocked;

        public int Columns { get; }
        public int Rows { get; }
        public float CellSize { get; }
        public float MinCoordinate { get; }
        public IReadOnlyList<Obstacle> Obstacles => _obstacles;

        public NavGrid(ArenaSettings arena, NavigationSettings nav, List<Obstacle> obstacles)
        {
            _arena = arena;
            _nav = nav;
            _obstacles = obstacles;
            CellSize = nav.CellSize;
            MinCoordinate = -arena.Half;
            Columns = (int)System.Math.Ceiling(arena.Half * 2f / CellSize);
            Rows = Columns;
            _blocked = new bool[Columns * Rows];
            Rebuild();
        }

        public void Rebuild()
        {
            float clearance = _nav.AgentClearance;
            for (int gy = 0; gy < Rows; gy++)
            {
                for (int gx = 0; gx < Columns; gx++)
                {
                    Vec2 p = new Vec2(CellCenterX(gx), CellCenterY(gy));
                    bool bad = !IsInsideArena(p, clearance);
                    if (!bad)
                    {
                        for (int i = 0; i < _obstacles.Count; i++)
                        {
                            Obstacle o = _obstacles[i];
                            if (Vec2.SqrDistance(p, o.Position) < (o.Radius + clearance) * (o.Radius + clearance))
                            {
                                bad = true;
                                break;
                            }
                        }
                    }
                    _blocked[gy * Columns + gx] = bad;
                }
            }
        }

        /// <summary>
        /// Octagon containment: inside the square AND inside the four chamfers.
        /// </summary>
        public bool IsInsideArena(Vec2 p, float margin = 0f)
        {
            float h = _arena.Half - margin;
            if (p.X < -h || p.X > h || p.Y < -h || p.Y > h) return false;
            float diagonal = _arena.Half * 2f - _arena.CornerCut - margin * 1.42f;
            return GhMath.Abs(p.X) + GhMath.Abs(p.Y) <= diagonal;
        }

        public int ColumnAt(float x) =>
            GhMath.ClampInt((int)System.Math.Floor((x - MinCoordinate) / CellSize), 0, Columns - 1);

        public int RowAt(float y) =>
            GhMath.ClampInt((int)System.Math.Floor((y - MinCoordinate) / CellSize), 0, Rows - 1);

        public float CellCenterX(int gx) => MinCoordinate + (gx + 0.5f) * CellSize;
        public float CellCenterY(int gy) => MinCoordinate + (gy + 0.5f) * CellSize;
        public Vec2 CellCenter(int gx, int gy) => new Vec2(CellCenterX(gx), CellCenterY(gy));

        public bool IsBlocked(int gx, int gy)
        {
            if (gx < 0 || gy < 0 || gx >= Columns || gy >= Rows) return true;
            return _blocked[gy * Columns + gx];
        }

        /// <summary>Nearest walkable cell, spiralling out. Used when a goal sits inside a blocker.</summary>
        public bool TryFindNearestFree(int gx, int gy, out int freeX, out int freeY, int maxRadius = 14)
        {
            if (!IsBlocked(gx, gy))
            {
                freeX = gx;
                freeY = gy;
                return true;
            }
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
                        if (!IsBlocked(gx + dx, gy + dy))
                        {
                            freeX = gx + dx;
                            freeY = gy + dy;
                            return true;
                        }
                    }
                }
            }
            freeX = gx;
            freeY = gy;
            return false;
        }

        /// <summary>True when a straight run from a to b touches nothing static.</summary>
        public bool HasLineOfSight(Vec2 a, Vec2 b, float pad = -1f)
        {
            if (pad < 0f) pad = _nav.AgentClearance * 0.85f;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                Obstacle o = _obstacles[i];
                if (Geometry.SegmentHitsCircle(a, b, o.Position, o.Radius + pad)) return false;
            }

            float distance = Vec2.Distance(a, b);
            int steps = System.Math.Max(2, (int)System.Math.Ceiling(distance / 0.6f));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                if (!IsInsideArena(Vec2.Lerp(a, b, t), pad * 0.6f)) return false;
            }
            return true;
        }
    }
}
