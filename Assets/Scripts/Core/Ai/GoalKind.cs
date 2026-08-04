namespace GoldHunter.Core.Ai
{
    /// <summary>
    /// The seven things a bot can want. Every tick each is scored and the best
    /// one wins, which is why bots look like they are making decisions rather
    /// than following a script.
    /// </summary>
    public enum GoalKind
    {
        /// <summary>Siphon a coin popper.</summary>
        Mine = 0,

        /// <summary>Run the bag home and bank it.</summary>
        Bank = 1,

        /// <summary>Chase a player who is carrying gold.</summary>
        Hunt = 2,

        /// <summary>Go and spend at a shop.</summary>
        Shop = 3,

        /// <summary>Punch an enemy vault open (needs the Steal upgrade).</summary>
        Raid = 4,

        /// <summary>Scoop loose gold off the floor.</summary>
        Loot = 5,

        /// <summary>Break away from a nearby threat while loaded.</summary>
        Flee = 6,
    }
}
