using System;

namespace GoldHunter.Core.Config
{
    /// <summary>Match length and the end-game rush. Editable in the Inspector.</summary>
    [Serializable]
    public class MatchSettings
    {
        /// <summary>Match duration in seconds. 150 = the 2.5 minute round.</summary>
        public float Duration = 150f;

        /// <summary>Seconds of "3 - 2 - 1 - GO" before the clock starts.</summary>
        public float CountdownSeconds = 3f;

        /// <summary>Seconds remaining when the Gold Rush begins.</summary>
        public float RushAtSecondsLeft = 25f;

        /// <summary>Popper output multiplier during the rush.</summary>
        public float RushPopperMultiplier = 2.5f;

        /// <summary>One-off gold dumped into the motherlode when the rush starts.</summary>
        public float RushBurstGold = 60f;
    }
}
