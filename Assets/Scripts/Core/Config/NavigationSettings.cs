using System;

namespace GoldHunter.Core.Config
{
    /// <summary>Nav grid resolution and path-following tolerances for the NPCs.</summary>
    [Serializable]
    public class NavigationSettings
    {
        /// <summary>A* grid cell size in metres.</summary>
        public float CellSize = 1.4f;

        /// <summary>Blockers are inflated by this before rasterising, so paths never clip corners.</summary>
        public float AgentClearance = 1f;

        /// <summary>Seconds between repaths while following a goal.</summary>
        public float RepathInterval = 0.55f;

        /// <summary>Distance at which a waypoint counts as reached.</summary>
        public float WaypointReach = 1.1f;

        /// <summary>Safety valve on A* expansion.</summary>
        public int MaxSearchNodes = 20000;
    }
}
