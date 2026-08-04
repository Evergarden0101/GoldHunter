using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Input;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Simulation
{
    /// <summary>
    /// A prospector. Owns its own movement, punch state machine, bag and
    /// upgrades — anything that needs to see a second entity (resolving a hit,
    /// banking, buying) lives in <see cref="MatchSimulation"/> instead.
    ///
    /// Humans and NPCs are the same object driven by the same
    /// <see cref="IController"/>; there is no AI-only branch in here.
    /// </summary>
    public sealed class PlayerState
    {
        private readonly GameConfig _config;
        private readonly int[] _levels;
        private readonly Dictionary<int, float> _raidCooldowns = new Dictionary<int, float>();
        private readonly List<ItemId> _purchases = new List<ItemId>();

        public int Index { get; }
        public string Name { get; }
        public bool IsHuman { get; }
        public IController Controller { get; }
        public BaseCamp Home { get; }
        public NpcProfile Profile { get; set; }
        public PlayerStats Stats { get; } = new PlayerStats();

        public Vec2 Position;
        public Vec2 Velocity;
        public float Facing;

        /// <summary>Gold being carried. Worth nothing at the whistle — bank it.</summary>
        public float Bag;

        /// <summary>-3 (smallest) .. +3 (largest), moved by Scale Up / Scale Down.</summary>
        public int ScaleLevel { get; private set; }

        public bool CanSteal => _levels[(int)ItemId.Steal] > 0;
        public IReadOnlyList<ItemId> Purchases => _purchases;

        // ---- combat ----
        public AttackPhase Phase { get; private set; } = AttackPhase.Idle;
        public float PhaseTimer { get; private set; }
        public float AttackCooldown { get; private set; }
        public bool IsCharging { get; private set; }
        public float ChargeTime { get; private set; }

        /// <summary>0 for a jab, up to 1 for a full charge. Set when the punch launches.</summary>
        public float PunchPower { get; private set; }

        public float Stun { get; private set; }
        public float Invulnerability { get; private set; }

        /// <summary>Everything already struck by the current active frame.</summary>
        public readonly HashSet<object> HitSet = new HashSet<object>();

        // ---- dash ----
        public float DashTimer { get; private set; }
        public float DashCooldown { get; private set; }
        private Vec2 _dashDirection;

        // ---- shopping ----
        public Shop CurrentShop { get; set; }
        public int ShopSelection { get; set; }
        public float BuyHold { get; set; }
        public float CycleLock { get; private set; }

        // ---- presentation (read by views, never gameplay) ----
        public float Squash { get; private set; } = 1f;
        public float HitFlash { get; private set; }
        public float MiningGlow { get; private set; }
        public float DepositGlow { get; private set; }
        public float WalkPhase { get; private set; }

        public PlayerState(int index, string name, bool isHuman, IController controller,
                           BaseCamp home, Vec2 position, float facing, GameConfig config)
        {
            Index = index;
            Name = name;
            IsHuman = isHuman;
            Controller = controller;
            Home = home;
            Position = position;
            Facing = facing;
            _config = config;
            _levels = new int[System.Enum.GetValues(typeof(ItemId)).Length];
        }

        /* ------------------------------------------------------- derived stats */

        public float Scale => 1f + ScaleLevel * _config.Upgrade.ScaleStep;
        public float Radius => _config.Player.Radius * Scale;

        public float BagCapacity =>
            _config.Player.BagCapacity + _levels[(int)ItemId.GoldBagUp] * _config.Upgrade.BagPerLevel;

        public float BagSpace => GhMath.Max(0f, BagCapacity - Bag);
        public float BagFill => GhMath.Clamp01(Bag / BagCapacity);

        /// <summary>Multiplier on gold ripped and knockback dealt.</summary>
        public float AttackMultiplier =>
            (1f + _levels[(int)ItemId.AttackUp] * _config.Upgrade.AttackPerLevel)
            * (1f + ScaleLevel * _config.Upgrade.ScalePowerPerLevel);

        /// <summary>Multiplier on gold lost per hit; lower is better.</summary>
        public float DefenseMultiplier =>
            (float)System.Math.Pow(1f - _config.Upgrade.DefensePerLevel, _levels[(int)ItemId.DefenseUp]);

        public float Reach =>
            _config.Combat.PunchReach * (1f + ScaleLevel * _config.Upgrade.ScaleReachPerLevel);

        /// <summary>Multiplier on gold stolen from this player's vault; lower is better.</summary>
        public float CampArmor =>
            (float)System.Math.Pow(1f - _config.Upgrade.CampArmorPerLevel, _levels[(int)ItemId.BaseCampUp]);

        public float DepositRate =>
            _config.Camp.DepositRatePerSecond
            * (1f + _levels[(int)ItemId.BaseCampUp] * _config.Upgrade.CampDepositPerLevel);

        public float EndBonusRate =>
            _levels[(int)ItemId.BaseCampUp] * _config.Upgrade.CampEndBonusPerLevel;

        /// <summary>A heavy bag slows you down: carrying a fortune is a real risk.</summary>
        public float Speed
        {
            get
            {
                float load = 1f - _config.Player.FullBagSlowdown * BagFill;
                return _config.Player.Speed * (1f + ScaleLevel * _config.Upgrade.ScaleSpeedPerLevel) * load;
            }
        }

        public bool CanAct => Stun <= 0f;
        public bool IsBusy => Phase != AttackPhase.Idle;

        public float ChargeRatio
        {
            get
            {
                if (!IsCharging) return 0f;
                float span = _config.Combat.ChargeFull - _config.Combat.ChargeMinHold;
                return GhMath.Clamp01((ChargeTime - _config.Combat.ChargeMinHold) / span);
            }
        }

        /* ------------------------------------------------------------ upgrades */

        public int GetLevel(ItemId id)
        {
            if (id == ItemId.ScaleUp) return GhMath.ClampInt(ScaleLevel, 0, int.MaxValue);
            if (id == ItemId.ScaleDown) return GhMath.ClampInt(-ScaleLevel, 0, int.MaxValue);
            return _levels[(int)id];
        }

        /// <summary>Applies a bought upgrade. Pricing and funding happen in ShoppingService.</summary>
        public void ApplyUpgrade(ItemId id, int maxScale)
        {
            switch (id)
            {
                case ItemId.ScaleUp:
                    ScaleLevel = GhMath.ClampInt(ScaleLevel + 1, -maxScale, maxScale);
                    break;
                case ItemId.ScaleDown:
                    ScaleLevel = GhMath.ClampInt(ScaleLevel - 1, -maxScale, maxScale);
                    break;
                default:
                    _levels[(int)id]++;
                    break;
            }
            _purchases.Add(id);
        }

        /* ------------------------------------------------------------- raiding */

        public float RaidCooldownFor(int campOwnerIndex)
        {
            return _raidCooldowns.TryGetValue(campOwnerIndex, out float v) ? v : 0f;
        }

        public void StartRaidCooldown(int campOwnerIndex, float seconds)
        {
            _raidCooldowns[campOwnerIndex] = seconds;
        }

        private void TickRaidCooldowns(float dt)
        {
            if (_raidCooldowns.Count == 0) return;
            var keys = new List<int>(_raidCooldowns.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                float next = _raidCooldowns[keys[i]] - dt;
                if (next <= 0f) _raidCooldowns.Remove(keys[i]);
                else _raidCooldowns[keys[i]] = next;
            }
        }

        /* ---------------------------------------------------------------- gold */

        /// <summary>Adds gold up to the bag limit. Returns how much actually fit.</summary>
        public float AddGold(float amount)
        {
            float take = GhMath.Min(amount, BagSpace);
            Bag += take;
            return take;
        }

        public float RemoveGold(float amount)
        {
            float taken = GhMath.Min(Bag, amount);
            Bag -= taken;
            return taken;
        }

        /* ---------------------------------------------------------------- tick */

        public void Tick(float dt, MatchSimulation sim)
        {
            Stun = GhMath.Max(0f, Stun - dt);
            Invulnerability = GhMath.Max(0f, Invulnerability - dt);
            AttackCooldown = GhMath.Max(0f, AttackCooldown - dt);
            DashCooldown = GhMath.Max(0f, DashCooldown - dt);
            CycleLock = GhMath.Max(0f, CycleLock - dt);
            HitFlash = GhMath.Max(0f, HitFlash - dt * 4f);
            MiningGlow = GhMath.Max(0f, MiningGlow - dt * 3f);
            DepositGlow = GhMath.Max(0f, DepositGlow - dt * 3f);
            TickRaidCooldowns(dt);

            Vec2 move = Controller != null ? Controller.Move : Vec2.Zero;
            float moveMagnitude = move.Magnitude;
            if (moveMagnitude > 1f)
            {
                move /= moveMagnitude;
                moveMagnitude = 1f;
            }

            if (CanAct)
            {
                TickAttack(dt, sim);
                if (CurrentShop != null) TickShop(dt, sim);
                else if (Controller != null && Controller.Action.WasPressed
                         && DashCooldown <= 0f && Phase == AttackPhase.Idle)
                {
                    StartDash(move, sim);
                }
            }
            else
            {
                IsCharging = false;
                ChargeTime = 0f;
                BuyHold = 0f;
            }

            TickMovement(dt, move, moveMagnitude);
            TickPresentation(dt, moveMagnitude);
        }

        private void TickMovement(float dt, Vec2 move, float moveMagnitude)
        {
            float speed = Speed;
            if (IsCharging) speed *= _config.Combat.ChargeMoveSlow;
            if (Phase == AttackPhase.Windup || Phase == AttackPhase.Active) speed *= 0.35f;
            else if (Phase == AttackPhase.Recover) speed *= 0.6f;

            if (DashTimer > 0f)
            {
                DashTimer -= dt;
                Velocity = _dashDirection * _config.Player.DashSpeed;
            }
            else if (CanAct)
            {
                Vec2 target = move * speed;
                float rate = (moveMagnitude > 0.02f ? _config.Player.Acceleration : _config.Player.Friction)
                             / GhMath.Max(1f, speed) * 3.4f;
                Velocity = new Vec2(GhMath.Damp(Velocity.X, target.X, rate, dt),
                                    GhMath.Damp(Velocity.Y, target.Y, rate, dt));
            }
            else
            {
                // Stunned: keep sliding with the knockback.
                float decay = _config.Player.KnockbackDecay;
                Velocity = new Vec2(GhMath.Damp(Velocity.X, 0f, decay, dt),
                                    GhMath.Damp(Velocity.Y, 0f, decay, dt));
            }

            Position += Velocity * dt;
        }

        private void TickPresentation(float dt, float moveMagnitude)
        {
            float speed = Velocity.Magnitude;
            if (speed > 0.4f && CanAct)
            {
                WalkPhase += dt * (6f + speed * 1.5f);
                float target = Velocity.Angle;
                float turn = _config.Player.TurnRate * dt;
                Facing = GhMath.RotateToward(Facing, target,
                    IsCharging || Phase != AttackPhase.Idle ? turn * 0.35f : turn);
            }

            // Aim overrides facing while charging, so a smash can be steered.
            if (IsCharging && Controller != null && moveMagnitude > 0.2f)
            {
                Facing = GhMath.RotateToward(Facing, Controller.Move.Angle,
                    _config.Player.TurnRate * 0.9f * dt);
            }

            float targetSquash =
                Phase == AttackPhase.Windup ? 0.86f :
                Phase == AttackPhase.Active ? 1.18f :
                DashTimer > 0f ? 1.14f :
                1f + (float)System.Math.Sin(WalkPhase) * 0.05f * GhMath.Clamp01(speed / 5f);
            Squash = GhMath.Damp(Squash, targetSquash, 18f, dt);
        }

        private void StartDash(Vec2 move, MatchSimulation sim)
        {
            Vec2 dir = move;
            if (dir.Magnitude < 0.1f) dir = Vec2.FromAngle(Facing);
            _dashDirection = dir.Normalized;
            DashTimer = _config.Player.DashTime;
            DashCooldown = _config.Player.DashCooldown;
            Facing = _dashDirection.Angle;
            sim.Listener.OnDash(this);
        }

        private void TickAttack(float dt, MatchSimulation sim)
        {
            // Advance the state machine first so timings stay frame-exact.
            if (Phase != AttackPhase.Idle)
            {
                PhaseTimer -= dt;
                if (PhaseTimer <= 0f)
                {
                    if (Phase == AttackPhase.Windup)
                    {
                        Phase = AttackPhase.Active;
                        PhaseTimer = _config.Combat.PunchActive;
                        HitSet.Clear();
                        // Lunge into the swing.
                        float lunge = 5.5f * (1f + PunchPower * 1.6f);
                        Velocity += Vec2.FromAngle(Facing, lunge);
                        sim.Listener.OnPunchThrown(this, PunchPower);
                    }
                    else if (Phase == AttackPhase.Active)
                    {
                        Phase = AttackPhase.Recover;
                        PhaseTimer = _config.Combat.PunchRecover * (1f + PunchPower * 0.5f);
                        if (HitSet.Count == 0) sim.Listener.OnPunchWhiffed(this);
                    }
                    else
                    {
                        Phase = AttackPhase.Idle;
                    }
                }
                return;
            }

            if (Controller == null) return;

            bool canStartPunch = AttackCooldown <= 0f && CurrentShop == null;

            if (Controller.Attack.IsDown && canStartPunch)
            {
                IsCharging = true;
                ChargeTime += dt;
            }

            if (Controller.Attack.WasReleased && canStartPunch)
            {
                float held = Controller.Attack.ReleaseHoldTime;
                IsCharging = false;
                ChargeTime = 0f;

                float span = _config.Combat.ChargeFull - _config.Combat.ChargeMinHold;
                float ratio = GhMath.Clamp01((held - _config.Combat.ChargeMinHold) / span);
                PunchPower = held < _config.Combat.ChargeMinHold ? 0f : GhMath.Max(0.001f, ratio);

                Phase = AttackPhase.Windup;
                PhaseTimer = _config.Combat.PunchWindup * (1f + PunchPower * 1.7f);
                AttackCooldown = PunchPower > 0f
                    ? _config.Combat.ChargeCooldown
                    : _config.Combat.PunchCooldown;
            }

            if (!Controller.Attack.IsDown)
            {
                IsCharging = false;
                ChargeTime = 0f;
            }
        }

        private void TickShop(float dt, MatchSimulation sim)
        {
            if (Controller == null) return;

            if (Controller.Action.WasPressed && CycleLock <= 0f)
            {
                ShopSelection = (ShopSelection + 1) % _config.Catalogue.Count;
                CycleLock = _config.Shop.CycleCooldown;
                BuyHold = 0f;
            }

            if (Controller.Attack.IsDown)
            {
                BuyHold += dt;
                if (BuyHold >= _config.Shop.BuyHoldSeconds)
                {
                    BuyHold = 0f;
                    sim.TryBuy(this, _config.Catalogue.Items[ShopSelection].Id);
                }
            }
            else
            {
                BuyHold = 0f;
            }
        }

        /* -------------------------------------------------------------- combat */

        /// <summary>Where the current swing reaches, and how far.</summary>
        public void GetPunchOrigin(out Vec2 origin, out float range)
        {
            float bonus = PunchPower * (_config.Combat.ChargeReachBonus / _config.Combat.PunchReach);
            range = Radius + Reach * (1f + bonus);
            origin = Position + Vec2.FromAngle(Facing, range * 0.55f);
        }

        public void ApplyKnockback(Vec2 direction, float force)
        {
            float resist = 1f / (1f + _levels[(int)ItemId.DefenseUp] * 0.18f
                                    + GhMath.Max(0, ScaleLevel) * 0.15f);
            Velocity += direction * (force * resist);
        }

        /// <summary>Puts this player into the stunned, briefly invulnerable hit reaction.</summary>
        public void EnterHitReaction(float stunSeconds)
        {
            Stun = stunSeconds;
            Invulnerability = _config.Player.InvulnerabilityAfterHit;
            HitFlash = 1f;
            Squash = 1.35f;
            IsCharging = false;
            ChargeTime = 0f;
            Phase = AttackPhase.Idle;
            PhaseTimer = 0f;
        }

        public void MarkMining() => MiningGlow = 1f;
        public void MarkDepositing() => DepositGlow = 1f;
    }
}
