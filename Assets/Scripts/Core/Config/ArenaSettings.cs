using System;

namespace GoldHunter.Core.Config
{
    /// <summary>Arena dimensions. The playfield is an octagon: a square with chamfered corners.</summary>
    [Serializable]
    public class ArenaSettings
    {
        /// <summary>Half-extent in metres; the arena spans -Half .. +Half on both axes.</summary>
        public float Half = 35f;

        /// <summary>How much each corner is cut off, forming the octagon.</summary>
        public float CornerCut = 9f;

        /// <summary>Distance from the centre popper to each base camp.</summary>
        public float CampRadius = 25f;

        /// <summary>Restitution when a player is shoved off a wall or blocker.</summary>
        public float WallBounce = 0.25f;
    }
}
