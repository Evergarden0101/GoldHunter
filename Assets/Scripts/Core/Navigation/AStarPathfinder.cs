using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Navigation
{
    /// <summary>
    /// 8-way A* over a <see cref="NavGrid"/>, followed by line-of-sight string
    /// pulling so bots run clean diagonals instead of a grid staircase.
    /// </summary>
    public sealed class AStarPathfinder
    {
        private static readonly int[] NeighbourX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] NeighbourY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private static readonly float[] NeighbourCost =
        {
            1f, 1f, 1f, 1f, 1.41421356f, 1.41421356f, 1.41421356f, 1.41421356f,
        };

        private readonly NavGrid _grid;
        private readonly NavigationSettings _settings;
        private readonly float[] _gScore;
        private readonly int[] _cameFrom;
        private readonly bool[] _closed;
        private readonly MinHeap _open = new MinHeap();
        private readonly List<Vec2> _raw = new List<Vec2>();

        public AStarPathfinder(NavGrid grid, NavigationSettings settings)
        {
            _grid = grid;
            _settings = settings;
            int total = grid.Columns * grid.Rows;
            _gScore = new float[total];
            _cameFrom = new int[total];
            _closed = new bool[total];
        }

        /// <summary>
        /// Fills <paramref name="result"/> with world-space waypoints (excluding the
        /// start). Returns false when the goal is unreachable.
        /// </summary>
        public bool TryFindPath(Vec2 start, Vec2 goal, List<Vec2> result)
        {
            result.Clear();

            // Straight shot? Skip the search entirely — this is the common case.
            if (_grid.HasLineOfSight(start, goal))
            {
                result.Add(goal);
                return true;
            }

            if (!_grid.TryFindNearestFree(_grid.ColumnAt(start.X), _grid.RowAt(start.Y),
                    out int sx, out int sy)) return false;
            if (!_grid.TryFindNearestFree(_grid.ColumnAt(goal.X), _grid.RowAt(goal.Y),
                    out int gx, out int gy)) return false;

            int columns = _grid.Columns;
            int startIndex = sy * columns + sx;
            int goalIndex = gy * columns + gx;
            if (startIndex == goalIndex)
            {
                result.Add(goal);
                return true;
            }

            for (int i = 0; i < _gScore.Length; i++)
            {
                _gScore[i] = float.PositiveInfinity;
                _cameFrom[i] = -1;
                _closed[i] = false;
            }
            _open.Clear();
            _gScore[startIndex] = 0f;
            _open.Push(startIndex, Heuristic(startIndex, goalIndex, columns));

            bool found = false;
            int expanded = 0;
            while (_open.Count > 0 && expanded++ < _settings.MaxSearchNodes)
            {
                int current = _open.Pop();
                if (_closed[current]) continue;
                _closed[current] = true;
                if (current == goalIndex)
                {
                    found = true;
                    break;
                }

                int cx = current % columns;
                int cy = current / columns;
                for (int n = 0; n < 8; n++)
                {
                    int nx = cx + NeighbourX[n];
                    int ny = cy + NeighbourY[n];
                    if (_grid.IsBlocked(nx, ny)) continue;

                    // No squeezing diagonally between two blockers.
                    if (NeighbourX[n] != 0 && NeighbourY[n] != 0 &&
                        (_grid.IsBlocked(cx + NeighbourX[n], cy) || _grid.IsBlocked(cx, cy + NeighbourY[n])))
                    {
                        continue;
                    }

                    int neighbour = ny * columns + nx;
                    if (_closed[neighbour]) continue;

                    float tentative = _gScore[current] + NeighbourCost[n];
                    if (tentative < _gScore[neighbour])
                    {
                        _gScore[neighbour] = tentative;
                        _cameFrom[neighbour] = current;
                        _open.Push(neighbour, tentative + Heuristic(neighbour, goalIndex, columns));
                    }
                }
            }

            if (!found) return false;

            // Reconstruct back to the start.
            _raw.Clear();
            int node = goalIndex;
            while (node != -1)
            {
                _raw.Add(_grid.CellCenter(node % columns, node / columns));
                if (node == startIndex) break;
                node = _cameFrom[node];
            }
            _raw.Reverse();
            _raw[0] = start;
            _raw.Add(goal);

            // String pulling: keep only the corners that vision actually requires.
            int anchor = 0;
            for (int k = 2; k < _raw.Count; k++)
            {
                if (!_grid.HasLineOfSight(_raw[anchor], _raw[k]))
                {
                    result.Add(_raw[k - 1]);
                    anchor = k - 1;
                }
            }
            result.Add(_raw[_raw.Count - 1]);
            return true;
        }

        private static float Heuristic(int from, int to, int columns)
        {
            int ax = from % columns, ay = from / columns;
            int bx = to % columns, by = to / columns;
            int dx = System.Math.Abs(ax - bx);
            int dy = System.Math.Abs(ay - by);
            // Octile distance.
            return (dx + dy) + (1.41421356f - 2f) * System.Math.Min(dx, dy);
        }
    }
}
