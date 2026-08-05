using GoldHunter.Core.Math;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>A punch that connected with another player.</summary>
    public struct PunchLandedEvent
    {
        public PlayerState Attacker;
        public PlayerState Victim;

        /// <summary>0 for a light jab, up to 1 for a full charge.</summary>
        public float Power;

        public bool IsCharged;

        /// <summary>Total gold ripped out of the victim's bag.</summary>
        public float GoldRipped;

        /// <summary>How much of that went straight into the attacker's bag.</summary>
        public float GoldTaken;

        /// <summary>The remainder, sprayed across the floor.</summary>
        public float GoldScattered;

        /// <summary>Midpoint of the impact, for effects.</summary>
        public Vec2 ImpactPoint;

        /// <summary>Direction from attacker to victim.</summary>
        public float ImpactAngle;

        /// <summary>Seconds of hit-stop this impact requested.</summary>
        public float HitStop;

        /// <summary>Screen shake trauma this impact requested, 0..1.</summary>
        public float Shake;
    }
}
