namespace GoldHunter.Core.Services
{
    /// <summary>What a world position can be standing on or next to.</summary>
    public enum InteractableKind
    {
        None = 0,

        /// <summary>Close enough to a coin popper to siphon it.</summary>
        CoinPopper = 1,

        /// <summary>Inside a shop's browse ring.</summary>
        Shop = 2,

        /// <summary>Inside your own camp — walking in here banks your bag.</summary>
        OwnBaseCamp = 3,

        /// <summary>Inside a rival's camp — punchable with the Steal upgrade.</summary>
        EnemyBaseCamp = 4,

        /// <summary>Blocked by scenery.</summary>
        Rock = 5,
    }
}
