using GoldHunter.Core.Config;
using UnityEngine;

namespace GoldHunter.Unity.Config
{
    /// <summary>
    /// How well bots execute, independent of what they want. Reaction time, aim
    /// jitter, charge discipline and speed.
    ///
    /// Create via: Assets > Create > GoldHunter > Difficulty
    /// </summary>
    [CreateAssetMenu(fileName = "Difficulty", menuName = "GoldHunter/Difficulty", order = 2)]
    public sealed class DifficultyAsset : ScriptableObject
    {
        [SerializeField] private string _label = "Normal";

        [Tooltip("Seconds between goal re-evaluations. Higher = slower to react.")]
        [Range(0.05f, 1f)] [SerializeField] private float _reactionTime = 0.24f;

        [Tooltip("Aim accuracy. Lower values add angular jitter to punches.")]
        [Range(0f, 1f)] [SerializeField] private float _aim = 0.82f;

        [Tooltip("How reliably a charged punch is released at the right moment.")]
        [Range(0f, 1f)] [SerializeField] private float _chargeSkill = 0.65f;

        [Range(0.5f, 1.5f)] [SerializeField] private float _speedMultiplier = 1f;

        public DifficultySettings ToSettings()
        {
            return new DifficultySettings
            {
                Label = _label,
                ReactionTime = _reactionTime,
                Aim = _aim,
                ChargeSkill = _chargeSkill,
                SpeedMultiplier = _speedMultiplier,
            };
        }
    }
}
