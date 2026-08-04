using System.Collections.Generic;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>
    /// Does nothing. Lets the simulation run headless (tests, balance sweeps)
    /// without every call site null-checking the listener.
    /// </summary>
    public sealed class NullSimulationListener : ISimulationListener
    {
        public static readonly NullSimulationListener Instance = new NullSimulationListener();

        public void OnPunchThrown(PlayerState attacker, float power) { }
        public void OnPunchWhiffed(PlayerState attacker) { }
        public void OnPunchLanded(in PunchLandedEvent evt) { }
        public void OnVaultRaided(in VaultRaidedEvent evt) { }
        public void OnPopperPunched(in PopperPunchedEvent evt) { }
        public void OnPopperGenerated(CoinPopper popper) { }
        public void OnMined(in MinedEvent evt) { }
        public void OnDeposited(in DepositEvent evt) { }
        public void OnPickupCollected(in PickupCollectedEvent evt) { }
        public void OnPurchase(in PurchaseEvent evt) { }
        public void OnPurchaseRejected(in PurchaseRejectedEvent evt) { }
        public void OnDash(PlayerState player) { }
        public void OnShopEntered(PlayerState player, Shop shop) { }
        public void OnShopExited(PlayerState player, Shop shop) { }
        public void OnAnnouncement(AnnouncementKind kind, string text) { }
        public void OnTicker(string text, int playerIndex) { }
        public void OnPhaseChanged(MatchPhase phase) { }
        public void OnMatchEnded(IReadOnlyList<MatchResultRow> results) { }
    }
}
