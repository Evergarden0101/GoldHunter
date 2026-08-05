using GoldHunter.Core.Math;
using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;
using UnityEngine;

namespace GoldHunter.Unity.Managers
{
    /// <summary>
    /// The scene's authority on the map.
    ///
    /// Everything that needs to ask "where is the arena, can a body stand here,
    /// and what is interactable at this point?" goes through this component
    /// rather than re-deriving the answer. It wraps the engine-independent
    /// <see cref="StageService"/> and adds the Unity-side conveniences:
    /// world-space conversion, screen-point queries and gizmo drawing.
    ///
    /// Attach next to <see cref="MatchManager"/>; it wires itself up on match start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageManager : MonoBehaviour
    {
        [Header("Debug drawing")]
        [SerializeField] private bool _drawArenaGizmos = true;
        [SerializeField] private bool _drawBlockerGizmos = true;
        [SerializeField] private bool _drawNavGrid;

        [Header("World placement")]
        [Tooltip("Metres to Unity units. 1 keeps the simulation's own scale.")]
        [SerializeField] private float _worldScale = 1f;

        [Tooltip("Simulation is 2D; this is the Y height everything is drawn at.")]
        [SerializeField] private float _groundHeight;

        private StageService _stage;

        public StageService Service => _stage;
        public bool IsReady => _stage != null;
        public float WorldScale => _worldScale;

        internal void Bind(StageService stage)
        {
            _stage = stage;
        }

        /* --------------------------------------------------- space conversion */

        /// <summary>Simulation metres to Unity world space (XZ plane).</summary>
        public Vector3 ToWorld(Vec2 point, float height = 0f)
        {
            return new Vector3(point.X * _worldScale, _groundHeight + height, point.Y * _worldScale);
        }

        /// <summary>Unity world space back to simulation metres.</summary>
        public Vec2 ToSimulation(Vector3 world)
        {
            return new Vec2(world.x / _worldScale, world.z / _worldScale);
        }

        /* ------------------------------------------------------------ map info */

        public float ArenaHalfExtent => _stage != null ? _stage.ArenaHalfExtent : 0f;

        /// <summary>True when the point lies inside the octagonal playfield.</summary>
        public bool IsInsideArena(Vec2 point, float margin = 0f)
        {
            return _stage != null && _stage.IsInsideArena(point, margin);
        }

        /// <summary>True when a body of this radius could stand here unobstructed.</summary>
        public bool IsWalkable(Vec2 point, float bodyRadius = 0f)
        {
            return _stage != null && _stage.IsWalkable(point, bodyRadius);
        }

        public bool IsWalkableWorld(Vector3 world, float bodyRadius = 0f)
        {
            return IsWalkable(ToSimulation(world), bodyRadius);
        }

        public bool HasLineOfSight(Vec2 from, Vec2 to)
        {
            return _stage != null && _stage.HasLineOfSight(from, to);
        }

        /* --------------------------------------------- interactable judgement */

        /// <summary>
        /// What can be interacted with at this position, for this player.
        /// Poppers win over shops, which win over camps — the order a player
        /// would read it when the rings overlap.
        /// </summary>
        public InteractableHit QueryInteractable(Vec2 point, float bodyRadius, int playerIndex)
        {
            return _stage != null
                ? _stage.QueryInteractable(point, bodyRadius, playerIndex)
                : InteractableHit.None;
        }

        public InteractableHit QueryInteractableWorld(Vector3 world, float bodyRadius, int playerIndex)
        {
            return QueryInteractable(ToSimulation(world), bodyRadius, playerIndex);
        }

        /// <summary>Convenience: is this position somewhere the given player can mine?</summary>
        public bool CanMineAt(Vec2 point, float bodyRadius, int playerIndex)
        {
            return QueryInteractable(point, bodyRadius, playerIndex).Kind == InteractableKind.CoinPopper;
        }

        /// <summary>Convenience: is this position inside the given player's own camp?</summary>
        public bool CanBankAt(Vec2 point, float bodyRadius, int playerIndex)
        {
            return QueryInteractable(point, bodyRadius, playerIndex).Kind == InteractableKind.OwnBaseCamp;
        }

        /// <summary>Convenience: is this position inside a shop's browse ring?</summary>
        public bool CanShopAt(Vec2 point, float bodyRadius, int playerIndex)
        {
            return QueryInteractable(point, bodyRadius, playerIndex).Kind == InteractableKind.Shop;
        }

        public CoinPopper NearestPopper(Vec2 point) => _stage?.NearestPopper(point);
        public Shop NearestShop(Vec2 point) => _stage?.NearestShop(point);

        /* ------------------------------------------------------------- gizmos */

        private void OnDrawGizmos()
        {
            if (_stage == null) return;

            if (_drawArenaGizmos)
            {
                Gizmos.color = new Color(0.45f, 0.55f, 0.8f, 0.9f);
                DrawArenaOutline();
            }

            if (_drawBlockerGizmos)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.5f);
                foreach (var obstacle in _stage.Obstacles)
                {
                    Gizmos.DrawWireSphere(ToWorld(obstacle.Position), obstacle.Radius * _worldScale);
                }

                // Camps are deliberately NOT blockers: they must stay walkable.
                Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
                foreach (var camp in _stage.Camps)
                {
                    Gizmos.DrawWireSphere(ToWorld(camp.Position), camp.Radius * _worldScale);
                }
            }

            if (_drawNavGrid) DrawNavGrid();
        }

        private void DrawArenaOutline()
        {
            float h = _stage.ArenaHalfExtent;
            float c = _stage.ArenaCornerCut;
            var corners = new[]
            {
                new Vec2(-h + c, -h), new Vec2(h - c, -h), new Vec2(h, -h + c), new Vec2(h, h - c),
                new Vec2(h - c, h), new Vec2(-h + c, h), new Vec2(-h, h - c), new Vec2(-h, -h + c),
            };
            for (int i = 0; i < corners.Length; i++)
            {
                Gizmos.DrawLine(ToWorld(corners[i]), ToWorld(corners[(i + 1) % corners.Length]));
            }
        }

        private void DrawNavGrid()
        {
            var grid = _stage.NavGrid;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
            for (int gy = 0; gy < grid.Rows; gy++)
            {
                for (int gx = 0; gx < grid.Columns; gx++)
                {
                    if (!grid.IsBlocked(gx, gy)) continue;
                    Vector3 centre = ToWorld(grid.CellCenter(gx, gy));
                    Gizmos.DrawWireCube(centre, new Vector3(grid.CellSize, 0.05f, grid.CellSize) * _worldScale);
                }
            }
        }
    }
}
