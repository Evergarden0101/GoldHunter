using System.Collections.Generic;
using System;
using GoldHunter.Core.Config;
using GoldHunter.Core.Events;
using GoldHunter.Core.Math;
using GoldHunter.Core.Services;

namespace GoldHunter.Core.Simulation
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

        /// <summary>
        /// The window a hold has to travel to go from a jab to a full smash.
        /// Derived so the charge readout and the punch that fires cannot
        /// disagree about how charged a punch was.
        /// </summary>
        public float ChargeSpan => ChargeFull - ChargeMinHold;
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

    /// <summary>
    /// Resolves active punch frames against players, vaults and coin poppers.
    ///
    /// This is the one place gold changes hands violently, so the conservation
    /// rule lives here too: whatever leaves a victim's bag either lands in the
    /// attacker's bag or hits the floor as a pickup. Nothing evaporates.
    /// </summary>
    public sealed class CombatResolver
    {
        private readonly GameConfig _config;
        private readonly BaseCampService _camps;
        private readonly ISimulationListener _listener;
        private readonly DeterministicRng _rng;

        public CombatResolver(GameConfig config, BaseCampService camps,
                              ISimulationListener listener, DeterministicRng rng)
        {
            _config = config;
            _camps = camps;
            _listener = listener;
            _rng = rng;
        }

        /// <summary>
        /// Checks every player whose swing is in its active frames.
        /// </summary>
        public void ResolveActivePunches(IReadOnlyList<PlayerState> players,
                                         IReadOnlyList<BaseCamp> camps,
                                         IReadOnlyList<CoinPopper> poppers,
                                         List<GoldPickup> pickups)
        {
            for (int i = 0; i < players.Count; i++)
            {
                PlayerState attacker = players[i];
                if (attacker.Phase != AttackPhase.Active) continue;

                attacker.GetPunchOrigin(out _, out float range);
                float power = attacker.PunchPower;

                ResolveAgainstPlayers(attacker, players, range, power, pickups);
                if (attacker.CanSteal) ResolveAgainstCamps(attacker, players, camps, range, power);
                ResolveAgainstPoppers(attacker, poppers, range, power, pickups);
            }
        }

        private void ResolveAgainstPlayers(PlayerState attacker, IReadOnlyList<PlayerState> players,
                                           float range, float power, List<GoldPickup> pickups)
        {
            for (int j = 0; j < players.Count; j++)
            {
                PlayerState victim = players[j];
                if (victim == attacker) continue;
                if (attacker.HitSet.Contains(victim)) continue;
                if (victim.Invulnerability > 0f) continue;

                float distance = Vec2.Distance(attacker.Position, victim.Position);
                if (distance > attacker.Radius + victim.Radius + range * 0.85f) continue;

                float angle = (victim.Position - attacker.Position).Angle;
                if (GhMath.Abs(GhMath.AngleDelta(attacker.Facing, angle))
                    > _config.Combat.PunchArc * 0.5f + 0.25f)
                {
                    continue;
                }

                attacker.HitSet.Add(victim);
                LandHit(attacker, victim, power, angle, pickups);
            }
        }

        private void LandHit(PlayerState attacker, PlayerState victim, float power,
                             float angle, List<GoldPickup> pickups)
        {
            CombatSettings combat = _config.Combat;
            bool charged = power > 0f;

            float fraction = charged
                ? GhMath.Lerp(combat.ChargedStealMin, combat.ChargedStealMax, power)
                : combat.LightStealFraction;
            fraction *= attacker.AttackMultiplier * victim.DefenseMultiplier;
            fraction = GhMath.Clamp(fraction, 0.05f, 0.95f);

            float amount = victim.Bag * fraction;
            if (victim.Bag > 0f) amount = GhMath.Max(amount, GhMath.Min(combat.MinSteal, victim.Bag));
            amount = (float)System.Math.Round(GhMath.Min(amount, victim.Bag));

            // Rounding can ask for slightly more than the bag holds, so the
            // amount that counts is what actually came out — anything else and
            // the difference gets scattered into existence.
            amount = victim.RemoveGold(amount);
            victim.Stats.Lost += amount;
            victim.Stats.PunchesTaken++;
            attacker.Stats.PunchesLanded++;

            float taken = attacker.AddGold((float)System.Math.Round(amount * combat.AttackerShare));
            attacker.Stats.Robbed += taken;
            float scattered = amount - taken;
            if (scattered > 0.5f)
            {
                ScatterGold(pickups, victim.Position, scattered, victim.Index, angle);
            }

            float force = (charged
                ? GhMath.Lerp(combat.KnockbackChargedMin, combat.KnockbackChargedMax, power)
                : combat.KnockbackLight) * attacker.AttackMultiplier;
            victim.ApplyKnockback(Vec2.FromAngle(angle), force);
            victim.EnterHitReaction(_config.Player.StunTime + (charged ? combat.StunChargedBonus * power : 0f));

            float hitStop = charged
                ? GhMath.Lerp(combat.HitStopChargedMin, combat.HitStopChargedMax, power)
                : combat.HitStopLight;
            float shake = charged
                ? GhMath.Lerp(combat.ShakeLight, combat.ShakeCharged, power)
                : combat.ShakeLight;

            var evt = new PunchLandedEvent
            {
                Attacker = attacker,
                Victim = victim,
                Power = power,
                IsCharged = charged,
                GoldRipped = amount,
                GoldTaken = taken,
                GoldScattered = scattered,
                ImpactPoint = Vec2.Lerp(attacker.Position, victim.Position, 0.5f),
                ImpactAngle = angle,
                HitStop = hitStop,
                Shake = shake,
            };
            _listener.OnPunchLanded(evt);
        }

        private void ResolveAgainstCamps(PlayerState attacker, IReadOnlyList<PlayerState> players,
                                         IReadOnlyList<BaseCamp> camps, float range, float power)
        {
            for (int c = 0; c < camps.Count; c++)
            {
                BaseCamp camp = camps[c];
                if (camp.OwnerIndex == attacker.Index) continue;
                if (attacker.HitSet.Contains(camp)) continue;

                float distance = Vec2.Distance(attacker.Position, camp.Position);
                if (distance > attacker.Radius + camp.Radius + range * 0.8f) continue;

                attacker.HitSet.Add(camp);

                if (_camps.TryRaid(attacker, camp, power, out float amount))
                {
                    _listener.OnVaultRaided(new VaultRaidedEvent
                    {
                        Thief = attacker,
                        Camp = camp,
                        Owner = players[camp.OwnerIndex],
                        Amount = amount,
                    });
                }
            }
        }

        private void ResolveAgainstPoppers(PlayerState attacker, IReadOnlyList<CoinPopper> poppers,
                                           float range, float power, List<GoldPickup> pickups)
        {
            for (int p = 0; p < poppers.Count; p++)
            {
                CoinPopper popper = poppers[p];
                if (attacker.HitSet.Contains(popper)) continue;

                float distance = Vec2.Distance(attacker.Position, popper.Position);
                if (distance > attacker.Radius + popper.Radius + range * 0.8f) continue;

                attacker.HitSet.Add(popper);

                if (popper.Gold <= 0f)
                {
                    popper.AddShake(0.5f);
                    continue;
                }

                // Round the request, not the result: rounding what the popper
                // already gave up would destroy the difference.
                float want = (float)System.Math.Round(
                    _config.Combat.PopperPunchGoldBase + power * _config.Combat.PopperPunchGoldCharged);
                float knocked = popper.KnockLoose(want);
                popper.AddShake(1.1f + power * 0.3f);

                float away = (popper.Position - attacker.Position).Angle + GhMath.Pi;
                ScatterGold(pickups, popper.Position, knocked, -1, away);

                _listener.OnPopperPunched(new PopperPunchedEvent
                {
                    Attacker = attacker,
                    Popper = popper,
                    GoldKnockedOut = knocked,
                    Power = power,
                });
            }
        }

        /// <summary>Explodes an amount of gold into physical blobs on the floor.</summary>
        public void ScatterGold(List<GoldPickup> pickups, Vec2 origin, float total,
                                int ownerIndex, float direction)
        {
            PickupSettings settings = _config.Pickup;
            float remaining = total;
            int guard = 0;

            // Blob amounts stay fractional and the last blob absorbs the remainder:
            // rounding here would quietly destroy gold and break conservation.
            while (remaining > 0.001f && guard++ < 40)
            {
                float amount = GhMath.Min(remaining, settings.ClumpSize * _rng.Range(0.7f, 1.3f));
                remaining -= amount;
                if (remaining < 0.5f)
                {
                    amount += remaining;
                    remaining = 0f;
                }

                float angle = direction + _rng.Range(-1.1f, 1.1f);
                float speed = _rng.Range(settings.ScatterSpeedMin, settings.ScatterSpeedMax);
                pickups.Add(new GoldPickup(
                    origin + Vec2.FromAngle(angle, 0.3f),
                    Vec2.FromAngle(angle, speed),
                    amount,
                    ownerIndex,
                    settings));
            }
        }
    }
}
