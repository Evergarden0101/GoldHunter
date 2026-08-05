using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>A thief with the Steal upgrade punched an enemy base camp open.</summary>
    public struct VaultRaidedEvent
    {
        public PlayerState Thief;
        public BaseCamp Camp;
        public PlayerState Owner;
        public float Amount;
    }
}
