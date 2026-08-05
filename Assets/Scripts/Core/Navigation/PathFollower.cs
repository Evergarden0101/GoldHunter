using System.Collections.Generic;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Navigation
{
    /// <summary>
    /// Walks an A* path: advances waypoints, greedily skips ahead when a later
    /// one is already visible, and repaths on a timer so bots react to a goal
    /// that moved.
    /// </summary>
    public sealed class PathFollower
    {
        private readonly AStarPathfinder _pathfinder;
        private readonly NavGrid _grid;
        private readonly NavigationSettings _settings;
        private readonly List<Vec2> _path = new List<Vec2>();

        private Vec2 _goal;
        private bool _hasGoal;
        private bool _needsRepath;
        private float _repathTimer;
        private int _index;

        public bool PathFailed { get; private set; }
        public IReadOnlyList<Vec2> Path => _path;
        public int WaypointIndex => _index;
        public bool HasGoal => _hasGoal;
        public Vec2 Goal => _goal;

        public PathFollower(AStarPathfinder pathfinder, NavGrid grid, NavigationSettings settings)
        {
            _pathfinder = pathfinder;
            _grid = grid;
            _settings = settings;
        }

        public void SetGoal(Vec2 goal, bool force = false)
        {
            if (!force && _hasGoal && Vec2.Distance(_goal, goal) < 1.2f) return;
            _goal = goal;
            _hasGoal = true;
            _needsRepath = true;
            _repathTimer = 0f;
        }

        public void Clear()
        {
            _hasGoal = false;
            _needsRepath = false;
            _path.Clear();
            _index = 0;
        }

        /// <summary>Desired unit direction for this frame, or zero when arrived.</summary>
        public Vec2 Steer(Vec2 position, float dt)
        {
            if (!_hasGoal) return Vec2.Zero;

            _repathTimer -= dt;
            if (_needsRepath || _repathTimer <= 0f || _path.Count == 0)
            {
                _needsRepath = false;
                _repathTimer = _settings.RepathInterval;
                bool ok = _pathfinder.TryFindPath(position, _goal, _path);
                _index = 0;
                PathFailed = !ok;
                if (!ok)
                {
                    // Unreachable: bee-line so the bot still commits to something.
                    return (_goal - position).Normalized;
                }
            }

            while (_index < _path.Count - 1 &&
                   Vec2.Distance(position, _path[_index]) < _settings.WaypointReach)
            {
                _index++;
            }

            for (int k = _path.Count - 1; k > _index; k--)
            {
                if (_grid.HasLineOfSight(position, _path[k]))
                {
                    _index = k;
                    break;
                }
            }

            Vec2 delta = _path[_index] - position;
            return delta.Magnitude < 0.05f ? Vec2.Zero : delta.Normalized;
        }
    }
}
