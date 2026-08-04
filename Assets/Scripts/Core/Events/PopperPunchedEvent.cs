using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>A punch shook coins loose from a popper onto the floor.</summary>
    public struct PopperPunchedEvent
    {
        public PlayerState Attacker;
        public CoinPopper Popper;
        public float GoldKnockedOut;
        public float Power;
    }
}
