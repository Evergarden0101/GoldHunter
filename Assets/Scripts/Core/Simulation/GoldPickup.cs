using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
    /// <summary>
    /// A blob of gold on the floor. Drifts, is magnetically drawn to nearby
    /// players, and evaporates if nobody claims it.
    /// </summary>
    public sealed class GoldPickup
    {
        private readonly PickupSettings _settings;

        public Vec2 Position;
        public Vec2 Velocity;
        public float Amount;
        public float Life;

        /// <summary>Who dropped it; they cannot instantly re-collect their own loss.</summary>
        public int OwnerIndex;

        public float OwnerLockout;
        public bool IsDead { get; private set; }

        public bool IsFading => Life < 3f;

        public GoldPickup(Vec2 position, Vec2 velocity, float amount, int ownerIndex, PickupSettings settings)
        {
            Position = position;
            Velocity = velocity;
            Amount = amount;
            OwnerIndex = ownerIndex;
            _settings = settings;
            Life = settings.Lifetime;
            OwnerLockout = settings.OwnerPickupDelay;
        }

        public void Tick(float dt, IReadOnlyList<PlayerState> players)
        {
            Life -= dt;
            OwnerLockout = GhMath.Max(0f, OwnerLockout - dt);
            if (Life <= 0f)
            {
                IsDead = true;
                return;
            }

            Velocity *= (float)System.Math.Exp(-_settings.Drag * dt);

            PlayerState best = null;
            float bestDistance = _settings.MagnetRange;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerState p = players[i];
                if (p.BagSpace <= 0f) continue;
                if (OwnerLockout > 0f && p.Index == OwnerIndex) continue;
                float d = Vec2.Distance(p.Position, Position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            if (best != null)
            {
                float pull = 1f - bestDistance / _settings.MagnetRange;
                Vec2 dir = (best.Position - Position).Normalized;
                Velocity += dir * (_settings.MagnetSpeed * pull * dt * 6f);
            }

            Position += Velocity * dt;
        }

        public void Kill() => IsDead = true;
    }
}
