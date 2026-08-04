using System;

namespace GoldHunter.Core.Config
{
    /// <summary>Movement, carrying and dash tuning shared by humans and NPCs.</summary>
    [Serializable]
    public class PlayerSettings
    {
        public float Radius = 1.2f;
        public float Speed = 6.2f;
        public float Acceleration = 46f;
        public float Friction = 12f;

        /// <summary>Gold a player can carry before an upgrade.</summary>
        public float BagCapacity = 40f;

        /// <summary>Speed penalty at a completely full bag (0.12 = 12% slower).</summary>
        public float FullBagSlowdown = 0.12f;

        public float TurnRate = 14f;
        public float StunTime = 0.42f;
        public float InvulnerabilityAfterHit = 0.55f;
        public float KnockbackDecay = 5.5f;

        public float DashSpeed = 15.5f;
        public float DashTime = 0.16f;
        public float DashCooldown = 2.4f;
    }
}
