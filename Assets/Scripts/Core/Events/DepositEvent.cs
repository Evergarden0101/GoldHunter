using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>Gold moved from a bag into its owner's vault.</summary>
    public struct DepositEvent
    {
        public PlayerState Player;
        public BaseCamp Camp;
        public float Amount;

        /// <summary>True on the tick that emptied the bag completely.</summary>
        public bool BagEmptied;
    }
}
