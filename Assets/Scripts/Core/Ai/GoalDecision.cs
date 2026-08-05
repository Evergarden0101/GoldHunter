using GoldHunter.Core.Math;

namespace GoldHunter.Core.Ai
{
    /// <summary>The winning goal for this decision tick, and where it points.</summary>
    public struct GoalDecision
    {
        public GoalKind Kind;
        public float Score;

        /// <summary>The thing being pursued, when there is one (player, popper, camp...).</summary>
        public object Target;
    }
}
