using GoldHunter.Core.Ai;
using GoldHunter.Core.Input;

namespace GoldHunter.Core.Simulation
{
    /// <summary>One seat's configuration, filled in by the lobby before kickoff.</summary>
    public sealed class PlayerSlot
    {
        public PlayerSlotKind Kind = PlayerSlotKind.Npc;
        public string DisplayName = "P1";

        /// <summary>Keyboard, gamepad or virtual — the simulation does not care which.</summary>
        public IController Controller;

        /// <summary>Personality, for NPC seats.</summary>
        public NpcProfile Profile;
    }
}
