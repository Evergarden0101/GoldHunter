using GoldHunter.Core.Config;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
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
