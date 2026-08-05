using System.Collections.Generic;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>
    /// How the simulation talks to the outside world.
    ///
    /// The core never spawns a particle, plays a sound or shakes a camera — it
    /// reports what happened and the Unity layer decides how that looks. This is
    /// what keeps the simulation compilable and testable without an engine.
    /// </summary>
    public interface ISimulationListener
    {
        void OnPunchThrown(PlayerState attacker, float power);
        void OnPunchWhiffed(PlayerState attacker);
        void OnPunchLanded(in PunchLandedEvent evt);
        void OnVaultRaided(in VaultRaidedEvent evt);
        void OnPopperPunched(in PopperPunchedEvent evt);

        /// <summary>Fired when a popper's generation timer produces a visible jolt.</summary>
        void OnPopperGenerated(CoinPopper popper);

        void OnMined(in MinedEvent evt);
        void OnDeposited(in DepositEvent evt);
        void OnPickupCollected(in PickupCollectedEvent evt);

        void OnPurchase(in PurchaseEvent evt);
        void OnPurchaseRejected(in PurchaseRejectedEvent evt);

        void OnDash(PlayerState player);
        void OnShopEntered(PlayerState player, Shop shop);
        void OnShopExited(PlayerState player, Shop shop);

        void OnAnnouncement(AnnouncementKind kind, string text);
        void OnTicker(string text, int playerIndex);
        void OnPhaseChanged(MatchPhase phase);
        void OnMatchEnded(IReadOnlyList<MatchResultRow> results);
    }
}
