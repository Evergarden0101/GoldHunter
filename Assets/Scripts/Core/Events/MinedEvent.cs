using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>Gold siphoned out of a coin popper this tick.</summary>
    public struct MinedEvent
    {
        public PlayerState Player;
        public CoinPopper Popper;
        public float Amount;
    }
}
