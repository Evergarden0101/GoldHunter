using System.Collections.Generic;
using System;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
    /// <summary>Loose gold lying on the floor after a punch or a popper hit.</summary>
    [Serializable]
    public class PickupSettings
    {
        public float Radius = 0.42f;
        public float MagnetRange = 2.2f;
        public float MagnetSpeed = 13f;

        /// <summary>Seconds before an uncollected coin blob evaporates.</summary>
        public float Lifetime = 22f;

        public float ScatterSpeedMin = 3.5f;
        public float ScatterSpeedMax = 8.5f;
        public float Drag = 3.4f;

        /// <summary>Gold per dropped blob; larger drops split into several.</summary>
        public float ClumpSize = 12f;

        /// <summary>Grace period before the victim can re-collect their own dropped gold.</summary>
        public float OwnerPickupDelay = 0.35f;

        /// <summary>Safety cap so the floor cannot fill with thousands of blobs.</summary>
        public int MaxActive = 160;
    }

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
    }
}
