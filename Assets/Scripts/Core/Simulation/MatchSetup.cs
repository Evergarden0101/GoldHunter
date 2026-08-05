using System.Collections.Generic;
using GoldHunter.Core.Ai;
using GoldHunter.Core.Config;

namespace GoldHunter.Core.Simulation
{
    /// <summary>Everything needed to start a match.</summary>
    public sealed class MatchSetup
    {
        public GameConfig Config = GameConfig.Default();
        public DifficultySettings Difficulty = DifficultySettings.Normal();
        public List<PlayerSlot> Slots = new List<PlayerSlot>();

        /// <summary>Seed for every random decision, so a match can be replayed exactly.</summary>
        public int Seed = 12345;
    }
}
