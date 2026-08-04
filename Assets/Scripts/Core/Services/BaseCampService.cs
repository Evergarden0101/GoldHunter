using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Math;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Services
{
    /// <summary>
    /// Owns the vaults: banking, raiding, standings and the final score.
    ///
    /// Only vault gold counts at the whistle. Anything still in a bag is worth
    /// nothing, which is the pressure the whole match is built around.
    /// </summary>
    public sealed class BaseCampService
    {
        private readonly GameConfig _config;
        private readonly IReadOnlyList<BaseCamp> _camps;
        private readonly IReadOnlyList<PlayerState> _players;

        public IReadOnlyList<BaseCamp> Camps => _camps;

        public BaseCampService(GameConfig config, IReadOnlyList<BaseCamp> camps,
                               IReadOnlyList<PlayerState> players)
        {
            _config = config;
            _camps = camps;
            _players = players;
        }

        public BaseCamp CampOf(int playerIndex) => _camps[playerIndex];

        public void Tick(float dt)
        {
            for (int i = 0; i < _camps.Count; i++) _camps[i].Tick(dt);
        }

        /// <summary>
        /// Moves gold from a bag into its owner's vault at the player's deposit
        /// rate. Returns the amount banked this tick.
        /// </summary>
        public float Deposit(PlayerState player, float dt)
        {
            if (player.Bag <= 0f) return 0f;
            float moved = GhMath.Min(player.Bag, player.DepositRate * dt);
            if (moved <= 0f) return 0f;

            player.Bag -= moved;
            player.Home.Deposit(moved);
            player.Stats.Banked += moved;
            player.MarkDepositing();
            if (player.Bag < 0.01f) player.Bag = 0f;
            return moved;
        }

        /// <summary>How much a raid would take right now, before it is attempted.</summary>
        public float PreviewRaid(PlayerState thief, BaseCamp camp, float punchPower)
        {
            PlayerState owner = _players[camp.OwnerIndex];
            float raw = GhMath.Max(
                _config.Camp.StealMin,
                camp.Vault * _config.Camp.StealFraction * (1f + punchPower * 0.5f) * owner.CampArmor);
            return GhMath.Min(GhMath.Min(raw, _config.Camp.StealCap),
                              GhMath.Min(camp.Vault, thief.BagSpace));
        }

        /// <summary>
        /// Attempts a vault raid. Requires the Steal upgrade and a spent cooldown.
        /// </summary>
        public bool TryRaid(PlayerState thief, BaseCamp camp, float punchPower, out float amount)
        {
            amount = 0f;
            if (!thief.CanSteal) return false;
            if (camp.OwnerIndex == thief.Index) return false;
            if (thief.RaidCooldownFor(camp.OwnerIndex) > 0f) return false;

            amount = (float)System.Math.Round(PreviewRaid(thief, camp, punchPower));
            if (amount <= 0f) return false;

            // Move gold, never mint it: take only what the vault holds, and hand
            // back anything the thief's bag could not actually fit.
            float withdrawn = camp.Withdraw(amount);
            float carried = thief.AddGold(withdrawn);
            if (carried < withdrawn) camp.Deposit(withdrawn - carried);
            amount = carried;
            if (amount <= 0f) return false;

            thief.Stats.VaultRaids++;
            thief.Stats.RaidedFor += amount;
            _players[camp.OwnerIndex].Stats.Lost += amount;
            thief.StartRaidCooldown(camp.OwnerIndex, _config.Camp.StealCooldown);
            camp.RaiseAlarm();
            return true;
        }

        /// <summary>Index of whoever currently has the most banked. Bots gang up on them.</summary>
        public int LeaderIndex()
        {
            int best = 0;
            for (int i = 1; i < _camps.Count; i++)
            {
                if (_camps[i].Vault > _camps[best].Vault) best = i;
            }
            return best;
        }

        public float TotalBanked()
        {
            float sum = 0f;
            for (int i = 0; i < _camps.Count; i++) sum += _camps[i].Vault;
            return sum;
        }

        /// <summary>Final score: the vault plus the Base Camp Up end bonus.</summary>
        public float FinalScore(PlayerState player)
        {
            BaseCamp camp = _camps[player.Index];
            return camp.Vault + camp.Vault * player.EndBonusRate;
        }
    }
}
