using System;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
    /// <summary>
    /// Per-kind coin popper tuning. Every field here is surfaced in the Unity
    /// Inspector, so popping speed can be retuned without touching code.
    /// </summary>
    [Serializable]
    public class CoinPopperSettings
    {
        /// <summary>Gold present the moment the match starts.</summary>
        public float StartingGold = 50f;

        /// <summary>Generation rate. This is the "coin popping speed".</summary>
        public float GoldPerMinute = 200f;

        /// <summary>Maximum gold the machine will hold.</summary>
        public float Capacity = 320f;

        /// <summary>Gold per second siphoned into a bag by a player standing in range.</summary>
        public float HarvestRatePerSecond = 34f;

        /// <summary>Physical body radius (solid).</summary>
        public float Radius = 2.5f;

        /// <summary>How far from the centre a player can stand and still mine.</summary>
        public float HarvestRange = 3.9f;

        /// <summary>Seconds between visible "pop" jolts while generating.</summary>
        public float PopInterval = 0.34f;

        public float GoldPerSecond => GoldPerMinute / 60f;

        public static CoinPopperSettings Motherlode() => new CoinPopperSettings
        {
            StartingGold = 50f,
            GoldPerMinute = 200f,
            Capacity = 320f,
            HarvestRatePerSecond = 34f,
            Radius = 2.5f,
            HarvestRange = 3.9f,
            PopInterval = 0.34f,
        };

        public static CoinPopperSettings Small() => new CoinPopperSettings
        {
            StartingGold = 20f,
            GoldPerMinute = 80f,
            Capacity = 160f,
            HarvestRatePerSecond = 26f,
            Radius = 1.7f,
            HarvestRange = 3.1f,
            PopInterval = 0.6f,
        };
    }

    /// <summary>Coin poppers come in two sizes with independent tuning.</summary>
    public enum PopperKind
    {
        Motherlode = 0,
        Small = 1,
    }

    /// <summary>
    /// A gold machine. Generates continuously at its configured rate, can be
    /// siphoned by anyone standing in range, and rattles when worked or hit.
    ///
    /// The shake value is simulation state rather than a view detail so every
    /// renderer (and the tests) sees the same thing.
    /// </summary>
    public sealed class CoinPopper
    {
        private readonly CoinPopperSettings _settings;
        private float _generationCarry;
        private float _popTimer;

        public PopperKind Kind { get; }
        public Vec2 Position { get; }
        public string Label { get; }
        public float Gold { get; private set; }

        /// <summary>0..1.4 shake energy, driven by mining, generating and punches.</summary>
        public float Shake { get; private set; }

        public float Radius => _settings.Radius;
        public float HarvestRange => _settings.HarvestRange;
        public float Capacity => _settings.Capacity;
        public float GoldPerMinute => _settings.GoldPerMinute;
        public float FillRatio => _settings.Capacity <= 0f ? 0f : GhMath.Clamp01(Gold / _settings.Capacity);

        public CoinPopper(PopperKind kind, Vec2 position, CoinPopperSettings settings, string label)
        {
            Kind = kind;
            Position = position;
            _settings = settings;
            Label = label;
            Gold = settings.StartingGold;
        }

        public void AddShake(float amount)
        {
            Shake = GhMath.Clamp(Shake + amount, 0f, 1.4f);
        }

        /// <summary>
        /// Ticks generation and shake decay.
        /// </summary>
        /// <param name="rateMultiplier">2.5x during the Gold Rush, 0 while paused.</param>
        /// <returns>True on a tick that produced a visible jolt.</returns>
        public bool Tick(float dt, float rateMultiplier)
        {
            Shake = GhMath.Max(0f, Shake - dt * (2.4f + Shake * 1.8f));

            float before = Gold;
            _generationCarry += _settings.GoldPerSecond * rateMultiplier * dt;
            int whole = (int)_generationCarry;
            if (whole > 0)
            {
                _generationCarry -= whole;
                Gold = GhMath.Min(_settings.Capacity, Gold + whole);
            }

            _popTimer -= dt;
            bool jolted = Gold > before && _popTimer <= 0f && Gold < _settings.Capacity;
            if (jolted)
            {
                _popTimer = _settings.PopInterval;
                AddShake(0.2f);
            }
            return jolted;
        }

        /// <summary>Siphons gold into a bag. Returns how much actually moved.</summary>
        public float Harvest(float bagSpace, float dt)
        {
            if (Gold <= 0f || bagSpace <= 0f) return 0f;
            float take = GhMath.Min(_settings.HarvestRatePerSecond * dt, GhMath.Min(Gold, bagSpace));
            if (take <= 0f) return 0f;
            Gold -= take;
            AddShake(dt * 3.4f);
            return take;
        }

        /// <summary>Knocks gold onto the floor. Returns how much came loose.</summary>
        public float KnockLoose(float amount)
        {
            float taken = GhMath.Min(Gold, amount);
            Gold -= taken;
            return taken;
        }

        public void Inject(float amount)
        {
            Gold = GhMath.Min(_settings.Capacity, Gold + amount);
        }
    }
}
