using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Input;
using GoldHunter.Core.Math;
using GoldHunter.Core.Navigation;
using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Ai
{
    /// <summary>
    /// Utility-scoring NPC brain.
    ///
    /// It never moves a player or throws a punch itself: it writes into a
    /// <see cref="VirtualController"/> and <see cref="PlayerState"/> consumes
    /// that exactly as it consumes a keyboard. Keeping that boundary is what
    /// guarantees bots can only do things a human could also do.
    ///
    /// Every score has the shape <c>value / (travelTime * k + 1)</c>, scaled by
    /// personality weights. Small multipliers do a lot of work here — change one
    /// at a time and re-measure with the headless harness.
    /// </summary>
    public sealed class NpcBrain
    {
        private readonly PlayerState _player;
        private readonly NpcProfile _profile;
        private readonly DifficultySettings _difficulty;
        private readonly MatchSimulation _sim;
        private readonly PathFollower _follower;
        private readonly VirtualController _controller;
        private readonly ShopPlanner _planner;
        private readonly DeterministicRng _rng;

        private GoalKind _goal = GoalKind.Mine;
        private object _target;
        private float _thinkTimer;
        private float _shopCooldown;
        private float _actionPulse;
        private float _dashUrge;
        private float _stuckTimer;
        private Vec2 _lastPosition;
        private float _wanderAngle;
        private bool _holdingCharge;

        private ShopPlan _plan;
        private Shop _lastShop;
        private int _purchaseMark;

        public GoalKind Goal => _goal;
        public object Target => _target;
        public PathFollower Follower => _follower;
        public bool PathFailed => _follower.PathFailed;
        public float StuckTimer => _stuckTimer;

        public NpcBrain(PlayerState player, NpcProfile profile, DifficultySettings difficulty,
                        MatchSimulation sim, PathFollower follower, int seed)
        {
            _player = player;
            _profile = profile;
            _difficulty = difficulty;
            _sim = sim;
            _follower = follower;
            _controller = player.Controller as VirtualController;
            _planner = new ShopPlanner(player, profile, sim.Shopping, sim.Config);
            _rng = new DeterministicRng(seed);
            _lastPosition = player.Position;
            _wanderAngle = _rng.Angle();
        }

        private float TravelTime(Vec2 destination)
        {
            float distance = Vec2.Distance(_player.Position, destination);
            return distance / GhMath.Max(1.5f, _player.Speed * _difficulty.SpeedMultiplier);
        }

        /* ---------------------------------------------------------------- tick */

        public void Tick(float dt)
        {
            if (_controller == null) return;

            _shopCooldown = GhMath.Max(0f, _shopCooldown - dt);
            _actionPulse = GhMath.Max(0f, _actionPulse - dt);
            _dashUrge = GhMath.Max(0f, _dashUrge - dt);

            // A reaction delay is what separates Easy from Hard; it never changes
            // what the bot wants, only how fast it notices.
            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = _difficulty.ReactionTime + _rng.Range(0f, 0.12f);
                Evaluate();
            }

            _controller.WantAttack = false;
            _controller.WantAction = false;

            if (_player.CurrentShop != _lastShop)
            {
                _lastShop = _player.CurrentShop;
                if (_player.CurrentShop != null) _purchaseMark = _player.Purchases.Count;
            }

            if (_player.CurrentShop != null)
            {
                TickShopping(dt);
                return;
            }

            TickMovement(dt);
            TickCombat();
        }

        private void TickMovement(float dt)
        {
            Vec2 destination = DestinationFor(_goal, _target);
            if (destination.SqrMagnitude > 0f || _target != null) _follower.SetGoal(destination);

            Vec2 steer = _follower.Steer(_player.Position, dt);

            // Local avoidance so bots converging on a popper don't form a scrum.
            Vec2 separation = Steering.Separation(
                _player.Position, _sim.PlayerPositions, _player.Index, _player.Radius * 2.6f);
            steer += separation * 0.85f;

            // Anti-stuck: if we want to move but aren't, jitter and force a repath.
            float moved = Vec2.Distance(_player.Position, _lastPosition);
            _lastPosition = _player.Position;
            if (moved < 0.02f && steer.SqrMagnitude > 0f)
            {
                _stuckTimer += dt;
                if (_stuckTimer > 0.5f)
                {
                    _wanderAngle += _rng.Range(1.4f, 2.6f);
                    steer += Vec2.FromAngle(_wanderAngle, 1.4f);
                    _follower.SetGoal(destination, true);
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }

            _controller.DesiredMove = steer.Normalized;
        }

        /* ------------------------------------------------------- goal scoring */

        private void Evaluate()
        {
            PlayerState me = _player;
            float timeLeft = _sim.TimeRemaining;
            float homeTravel = TravelTime(me.Home.Position);

            // Bagged gold scores nothing, so past this point banking is all that matters.
            bool endgame = timeLeft < homeTravel + 4f;

            var best = new GoalDecision { Kind = GoalKind.Mine, Score = float.NegativeInfinity };

            _plan = (_shopCooldown <= 0f && !endgame)
                ? _planner.Plan(_sim.LeaderIndex, timeLeft, _sim.Config.Match.Duration)
                : default;

            ScoreBank(ref best, endgame, homeTravel);
            ScoreMine(ref best, endgame);
            ScoreHunt(ref best);
            ScoreShop(ref best);
            ScoreRaid(ref best, endgame);
            ScoreLoot(ref best);
            ScoreFlee(ref best);

            if (float.IsNegativeInfinity(best.Score))
            {
                best.Kind = GoalKind.Mine;
                best.Target = _sim.Poppers.Count > 0 ? _sim.Poppers[0] : null;
            }

            _goal = best.Kind;
            _target = best.Target;
        }

        private static void Consider(ref GoalDecision best, GoalKind kind, float score, object target)
        {
            if (score <= best.Score) return;
            best.Kind = kind;
            best.Score = score;
            best.Target = target;
        }

        private void ScoreBank(ref GoalDecision best, bool endgame, float homeTravel)
        {
            PlayerState me = _player;
            if (me.Bag <= 0f) return;

            float fill = me.Bag / me.BagCapacity;
            float score = (me.Bag / 35f) * (0.6f + _profile.SaveGoldWill * 1.3f) / (homeTravel * 0.5f + 1f);
            score *= 0.6f + fill * 1.3f;

            if (endgame) score += 100f;                       // nothing else can matter now
            if (me.BagSpace <= 1f) score *= 1.8f;             // full bag: no reason to stay out
            if (me.Home.Vault < 50f) score *= 1.5f;           // get something on the board first

            Consider(ref best, GoalKind.Bank, score, me.Home);
        }

        private void ScoreMine(ref GoalDecision best, bool endgame)
        {
            PlayerState me = _player;
            if (me.BagSpace <= 3f || endgame) return;

            for (int i = 0; i < _sim.Poppers.Count; i++)
            {
                CoinPopper popper = _sim.Poppers[i];
                float gettable = GhMath.Min(popper.Gold, me.BagSpace);
                if (gettable < 3f) continue;

                float travel = TravelTime(popper.Position);
                int contest = _sim.CountPlayersNear(popper.Position, popper.HarvestRange + 2.5f, me);

                float score = (gettable / 40f) * (0.9f + _profile.Greed * 0.5f) / (travel * 0.5f + 1f);
                score *= 1f - contest * 0.18f * (1f - _profile.AttackWill);
                if (popper.Kind == PopperKind.Motherlode) score *= 1.12f;

                // Hysteresis: stop bots dithering between two equally good machines.
                if (_goal == GoalKind.Mine && ReferenceEquals(_target, popper)) score *= 1.15f;

                Consider(ref best, GoalKind.Mine, score, popper);
            }
        }

        private void ScoreHunt(ref GoalDecision best)
        {
            PlayerState me = _player;
            for (int i = 0; i < _sim.Players.Count; i++)
            {
                PlayerState other = _sim.Players[i];
                if (other == me) continue;

                float loot = GhMath.Min(other.Bag, me.BagSpace);
                if (loot < 5f) continue;

                float travel = TravelTime(other.Position);
                if (travel > 3.2f && _profile.AttackWill < 0.7f) continue;

                // Chasing is expensive; it only wins when the mark is close and loaded.
                float score = (loot / 45f) * (0.3f + _profile.AttackWill * 1.5f) / (travel * 1.25f + 1f);
                if (other.Stun > 0f) score *= 1.5f;
                if (other.GetLevel(ItemId.DefenseUp) > me.GetLevel(ItemId.AttackUp) + 1) score *= 0.7f;
                if (other.Index == _sim.LeaderIndex) score *= 1.3f;   // gang up on whoever is winning
                if (_goal == GoalKind.Hunt && ReferenceEquals(_target, other)) score *= 1.2f;

                Consider(ref best, GoalKind.Hunt, score, other);
            }
        }

        private void ScoreShop(ref GoalDecision best)
        {
            if (!_plan.HasPlan) return;

            Shop shop = _sim.Stage.NearestShop(_player.Position);
            if (shop == null) return;

            float travel = TravelTime(shop.Position);
            float eager = _plan.IsDream ? 1.6f : 1f;
            float score = (0.85f + _profile.ShopWill * 1.4f) / (travel * 0.45f + 1f) * 1.25f * eager;

            Consider(ref best, GoalKind.Shop, score, shop);
        }

        private void ScoreRaid(ref GoalDecision best, bool endgame)
        {
            PlayerState me = _player;
            if (!me.CanSteal || me.BagSpace <= 4f || endgame) return;

            for (int i = 0; i < _sim.Camps.Camps.Count; i++)
            {
                BaseCamp camp = _sim.Camps.Camps[i];
                if (camp.OwnerIndex == me.Index) continue;
                if (me.RaidCooldownFor(camp.OwnerIndex) > 0f) continue;

                float loot = _sim.Camps.PreviewRaid(me, camp, 0f);
                if (loot < 6f) continue;

                float travel = TravelTime(camp.Position);
                PlayerState owner = _sim.Players[camp.OwnerIndex];
                float guarded = Vec2.Distance(owner.Position, camp.Position) < 8f ? 0.55f : 1f;

                // One punch on a stocked vault can out-earn a whole ore run —
                // which is the entire point of having paid for Steal.
                float score = (loot / 26f) * (0.55f + _profile.StealWill * 2.5f)
                              / (travel * 0.35f + 1f) * guarded;
                if (camp.OwnerIndex == _sim.LeaderIndex) score *= 1.3f;

                Consider(ref best, GoalKind.Raid, score, camp);
            }
        }

        private void ScoreLoot(ref GoalDecision best)
        {
            PlayerState me = _player;
            if (me.BagSpace <= 3f) return;

            for (int i = 0; i < _sim.Pickups.Count; i++)
            {
                GoldPickup pickup = _sim.Pickups[i];
                if (pickup.IsDead) continue;

                float travel = TravelTime(pickup.Position);
                if (travel > 2.2f) continue;

                float score = (pickup.Amount / 25f) * (0.9f + _profile.Greed) / (travel * 1.4f + 0.6f);
                Consider(ref best, GoalKind.Loot, score, pickup);
            }
        }

        private void ScoreFlee(ref GoalDecision best)
        {
            PlayerState me = _player;
            const float fleeRange = 6f;
            if (me.Bag <= me.BagCapacity * 0.5f) return;

            PlayerState threat = null;
            float threatDistance = float.MaxValue;
            for (int i = 0; i < _sim.Players.Count; i++)
            {
                PlayerState other = _sim.Players[i];
                if (other == me) continue;
                float d = Vec2.Distance(me.Position, other.Position);
                if (d < threatDistance)
                {
                    threatDistance = d;
                    threat = other;
                }
            }

            if (threat == null || threatDistance >= fleeRange || threat.Stun > 0f) return;

            float danger = (1f - threatDistance / fleeRange) * threat.AttackMultiplier;
            float score = danger * (0.3f + _profile.Caution * 1.5f) * (me.Bag / me.BagCapacity) * 0.9f;
            if (threat.IsCharging) score *= 1.6f;   // a wound-up smash is worth dodging

            Consider(ref best, GoalKind.Flee, score, me.Home);
        }

        /* ------------------------------------------------------- destinations */

        private Vec2 DestinationFor(GoalKind goal, object target)
        {
            PlayerState me = _player;
            switch (goal)
            {
                case GoalKind.Mine:
                {
                    // Stand just inside the harvest ring, on our side of the machine.
                    if (!(target is CoinPopper popper)) return me.Position;
                    float angle = (me.Position - popper.Position).Angle;
                    return popper.Position + Vec2.FromAngle(angle, popper.Radius + me.Radius + 0.5f);
                }
                case GoalKind.Bank:
                case GoalKind.Flee:
                    return me.Home.Position;

                case GoalKind.Hunt:
                {
                    // Lead the target slightly instead of chasing where they were.
                    if (!(target is PlayerState victim)) return me.Position;
                    return victim.Position + victim.Velocity * (_difficulty.Aim * 0.35f);
                }
                case GoalKind.Shop:
                    return target is Shop shop ? shop.Position : me.Position;

                case GoalKind.Raid:
                    return target is BaseCamp camp ? camp.Position : me.Position;

                case GoalKind.Loot:
                    return target is GoldPickup pickup ? pickup.Position : me.Position;

                default:
                    return me.Position;
            }
        }

        /* ----------------------------------------------------------- shopping */

        private void TickShopping(float dt)
        {
            PlayerState me = _player;

            if (!_plan.HasPlan || _sim.Shopping.IsMaxed(me, _plan.Item)
                || !_sim.Shopping.CanAfford(me, _plan.Item))
            {
                LeaveShop();
                return;
            }

            int index = _sim.Config.Catalogue.IndexOf(_plan.Item);
            if (index < 0)
            {
                LeaveShop();
                return;
            }

            if (me.ShopSelection != index)
            {
                // The selection advances on a rising edge, so the press must be pulsed.
                _actionPulse -= dt;
                _controller.WantAction = _actionPulse <= 0f;
                if (_controller.WantAction) _actionPulse = _sim.Config.Shop.CycleCooldown + 0.06f;
            }
            else
            {
                _controller.WantAttack = true;      // hold to confirm
                if (me.Purchases.Count > _purchaseMark)
                {
                    _purchaseMark = me.Purchases.Count;
                    _shopCooldown = 7f;
                    _plan = default;
                    LeaveShop();
                    return;
                }
            }

            _controller.DesiredMove = Vec2.Zero;
        }

        private void LeaveShop()
        {
            _shopCooldown = GhMath.Max(_shopCooldown, 3f);
            _controller.WantAttack = false;
            _controller.WantAction = false;
            if (_player.CurrentShop != null)
            {
                _controller.DesiredMove = (_player.Position - _player.CurrentShop.Position).Normalized;
            }
        }

        /* ------------------------------------------------------------- combat */

        private void TickCombat()
        {
            PlayerState me = _player;
            if (!me.CanAct || me.IsBusy)
            {
                _holdingCharge = false;
                return;
            }

            // Raiding: punch the vault itself.
            if (_goal == GoalKind.Raid && _target is BaseCamp camp)
            {
                float distance = Vec2.Distance(me.Position, camp.Position);
                if (distance < camp.Radius + me.Radius + me.Reach * 0.85f)
                {
                    AimAt(camp.Position);
                    _controller.WantAttack = true;    // light taps are enough on a vault
                    return;
                }
            }

            PlayerState victim = _goal == GoalKind.Hunt ? _target as PlayerState : FindAdjacentTarget();
            if (victim != null)
            {
                float distance = Vec2.Distance(me.Position, victim.Position);
                float reach = me.Radius + victim.Radius + me.Reach;
                bool worthCharging = victim.Bag > 25f && _rng.Next() < _difficulty.ChargeSkill;

                if (distance < reach * 2.4f && _rng.Next() < _difficulty.Aim) AimAt(victim.Position);

                if (distance < reach * 3f && distance > reach * 1.05f && me.DashCooldown <= 0f
                    && _dashUrge <= 0f && _rng.Next() < _difficulty.ChargeSkill * 0.5f)
                {
                    _controller.WantAction = true;    // dash in
                    _dashUrge = 1.6f;
                }

                if (_holdingCharge || worthCharging)
                {
                    _holdingCharge = true;
                    _controller.WantAttack = true;
                    bool fullyCharged = me.ChargeRatio >= 0.85f - (1f - _difficulty.ChargeSkill) * 0.5f;

                    if (distance <= reach * 0.95f && (fullyCharged || me.ChargeRatio > 0.35f))
                    {
                        _controller.WantAttack = false;   // release into the hit
                        _holdingCharge = false;
                    }
                    else if (me.ChargeTime > _sim.Config.Combat.ChargeFull * 1.9f)
                    {
                        _controller.WantAttack = false;   // never hold forever
                        _holdingCharge = false;
                    }
                }
                else if (distance <= reach * 0.95f)
                {
                    _controller.WantAttack = true;
                }
            }
            else
            {
                _holdingCharge = false;
            }

            if (_goal == GoalKind.Flee && me.DashCooldown <= 0f && _dashUrge <= 0f)
            {
                _controller.WantAction = true;
                _dashUrge = 1.2f;
            }
        }

        /// <summary>Someone standing right next to us with loot worth taking.</summary>
        private PlayerState FindAdjacentTarget()
        {
            PlayerState me = _player;
            if (_profile.AttackWill < 0.15f) return null;

            PlayerState best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _sim.Players.Count; i++)
            {
                PlayerState other = _sim.Players[i];
                if (other == me) continue;

                float distance = Vec2.Distance(me.Position, other.Position);
                float reach = me.Radius + other.Radius + me.Reach;
                if (distance < reach * 1.25f && other.Bag > 6f && me.BagSpace > 4f && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                }
            }
            return best;
        }

        /// <summary>
        /// Steers the aim through the movement stick, because facing is derived
        /// from it — the bot has no privileged way to point at someone.
        /// </summary>
        private void AimAt(Vec2 point)
        {
            float angle = (point - _player.Position).Angle;
            float jitter = (1f - _difficulty.Aim) * 0.5f;
            Vec2 aim = Vec2.FromAngle(angle + _rng.Range(-jitter, jitter));
            const float blend = 0.65f;
            _controller.DesiredMove = _controller.DesiredMove * (1f - blend) + aim * blend;
        }

        public string DebugLabel => _profile.Archetype + ":" + _goal;
    }
}
