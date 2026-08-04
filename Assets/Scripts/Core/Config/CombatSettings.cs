using System;

namespace GoldHunter.Core.Config
{
    /// <summary>Punch timings, how much gold a hit rips loose, and the impact feedback.</summary>
    [Serializable]
    public class CombatSettings
    {
        public float PunchWindup = 0.06f;
        public float PunchActive = 0.09f;
        public float PunchRecover = 0.2f;
        public float PunchCooldown = 0.28f;

        /// <summary>Reach added on top of the puncher's own radius.</summary>
        public float PunchReach = 1.55f;
        public float PunchArc = 1.948f;

        /// <summary>Hold longer than this and the punch becomes a charged smash.</summary>
        public float ChargeMinHold = 0.18f;

        /// <summary>Hold time for maximum charge.</summary>
        public float ChargeFull = 1.15f;
        public float ChargeMoveSlow = 0.42f;
        public float ChargeReachBonus = 0.9f;
        public float ChargeCooldown = 0.5f;

        /// <summary>Fraction of the victim's bag a light jab rips loose.</summary>
        public float LightStealFraction = 0.35f;
        public float ChargedStealMin = 0.45f;
        public float ChargedStealMax = 0.8f;

        /// <summary>Share of the loot that lands in the attacker's bag; the rest scatters.</summary>
        public float AttackerShare = 0.75f;

        /// <summary>A punch on an almost-empty bag still shakes this much loose.</summary>
        public float MinSteal = 4f;

        public float KnockbackLight = 9f;
        public float KnockbackChargedMin = 13f;
        public float KnockbackChargedMax = 24f;
        public float StunChargedBonus = 0.3f;

        /// <summary>Hit-stop freezes the simulation clock; presentation keeps real time.</summary>
        public float HitStopLight = 0.055f;
        public float HitStopChargedMin = 0.09f;
        public float HitStopChargedMax = 0.19f;

        public float ShakeLight = 0.28f;
        public float ShakeCharged = 0.75f;

        /// <summary>Gold knocked out of a coin popper by a punch (min + power scaling).</summary>
        public float PopperPunchGoldBase = 5f;
        public float PopperPunchGoldCharged = 11f;
    }
}
