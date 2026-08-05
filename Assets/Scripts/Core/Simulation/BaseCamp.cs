using System;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
    /// <summary>Vault behaviour: banking speed and how much a raid takes.</summary>
    [Serializable]
    public class BaseCampSettings
    {
        public float Radius = 3.2f;

        /// <summary>Gold per second moved from bag to vault while standing in your camp.</summary>
        public float DepositRatePerSecond = 95f;

        /// <summary>Seconds before the same thief can rob the same vault again.</summary>
        public float StealCooldown = 4.5f;

        /// <summary>Share of the vault taken by one successful raid.</summary>
        public float StealFraction = 0.25f;

        /// <summary>Hard ceiling on a single raid.</summary>
        public float StealCap = 70f;

        /// <summary>A raid always takes at least this much (if the vault holds it).</summary>
        public float StealMin = 10f;
    }

    /// <summary>
    /// A player's vault. Deliberately NOT a solid obstacle — it has to be
    /// walkable or its owner could never step in to deposit.
    /// </summary>
    public sealed class BaseCamp
    {
        private readonly BaseCampSettings _settings;

        public int OwnerIndex { get; }
        public Vec2 Position { get; }

        /// <summary>Banked gold. This, plus the end bonus, is the score.</summary>
        public float Vault { get; private set; }

        /// <summary>Shake energy from being raided.</summary>
        public float Shake { get; private set; }

        /// <summary>Counts down after a raid so the view can flash an alarm.</summary>
        public float Alarm { get; private set; }

        public float Radius => _settings.Radius;

        public BaseCamp(int ownerIndex, Vec2 position, BaseCampSettings settings)
        {
            OwnerIndex = ownerIndex;
            Position = position;
            _settings = settings;
        }

        public void Tick(float dt)
        {
            Shake = GhMath.Max(0f, Shake - dt * 3.2f);
            Alarm = GhMath.Max(0f, Alarm - dt);
        }

        public void Deposit(float amount)
        {
            Vault += amount;
            Shake = GhMath.Min(0.5f, Shake + amount * 0.004f);
        }

        /// <summary>Removes gold from the vault; used by raids and vault-funded purchases.</summary>
        public float Withdraw(float amount)
        {
            float taken = GhMath.Min(Vault, amount);
            Vault -= taken;
            return taken;
        }

        public void RaiseAlarm(float seconds = 1.6f)
        {
            Alarm = GhMath.Max(Alarm, seconds);
            Shake = 1f;
        }
    }
}
