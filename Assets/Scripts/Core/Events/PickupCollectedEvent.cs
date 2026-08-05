using GoldHunter.Core.Math;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Events
{
    /// <summary>Loose floor gold scooped up.</summary>
    public struct PickupCollectedEvent
    {
        public PlayerState Player;
        public Vec2 Position;
        public float Amount;
    }
}
