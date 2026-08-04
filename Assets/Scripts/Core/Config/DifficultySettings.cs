using System;

namespace GoldHunter.Core.Config
{
    /// <summary>
    /// Skill knobs that multiply on top of an <see cref="NpcProfile"/>.
    /// These change how well a bot executes, never what it wants.
    /// </summary>
    [Serializable]
    public class DifficultySettings
    {
        public string Label = "Normal";

        /// <summary>Seconds between goal re-evaluations. Higher = slower to react.</summary>
        public float ReactionTime = 0.24f;

        /// <summary>Aim accuracy 0..1. Lower values add angular jitter.</summary>
        public float Aim = 0.82f;

        /// <summary>How reliably the bot times a charged punch release, 0..1.</summary>
        public float ChargeSkill = 0.65f;

        /// <summary>Movement speed multiplier.</summary>
        public float SpeedMultiplier = 1f;

        public static DifficultySettings Easy() => new DifficultySettings
        { Label = "Easy", ReactionTime = 0.42f, Aim = 0.62f, ChargeSkill = 0.35f, SpeedMultiplier = 0.9f };

        public static DifficultySettings Normal() => new DifficultySettings
        { Label = "Normal", ReactionTime = 0.24f, Aim = 0.82f, ChargeSkill = 0.65f, SpeedMultiplier = 1f };

        public static DifficultySettings Hard() => new DifficultySettings
        { Label = "Hard", ReactionTime = 0.12f, Aim = 0.94f, ChargeSkill = 0.9f, SpeedMultiplier = 1.05f };
    }
}
