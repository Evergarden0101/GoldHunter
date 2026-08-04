using GoldHunter.Core.Config;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
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
        public CoinPopperSettings Settings => _settings;
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
