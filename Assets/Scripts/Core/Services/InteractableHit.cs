using GoldHunter.Core.Math;

namespace GoldHunter.Core.Services
{
    /// <summary>
    /// The answer to "what is at this position?". Returned by
    /// <see cref="StageService"/> so gameplay code and UI ask the same question
    /// in the same way.
    /// </summary>
    public struct InteractableHit
    {
        public InteractableKind Kind;

        /// <summary>Index into the matching collection (poppers, shops, camps, rocks).</summary>
        public int Index;

        public Vec2 Position;

        /// <summary>Distance from the query point to the interactable's centre.</summary>
        public float Distance;

        public bool Exists => Kind != InteractableKind.None;

        public static InteractableHit None => new InteractableHit { Kind = InteractableKind.None, Index = -1 };
    }
}
