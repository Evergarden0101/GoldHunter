using System.Collections.Generic;
using GoldHunter.Core.Ai;
using GoldHunter.Core.Services;
using UnityEngine;

namespace GoldHunter.Unity.Config
{
    /// <summary>
    /// An NPC personality as a tunable asset: attack will, save-gold will,
    /// steal will and per-item shopping taste. Duplicate the asset to invent a
    /// new archetype without writing any AI code.
    ///
    /// Create via: Assets > Create > GoldHunter > NPC Profile
    /// </summary>
    [CreateAssetMenu(fileName = "NpcProfile", menuName = "GoldHunter/NPC Profile", order = 1)]
    public sealed class NpcProfileAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName = "Pip";
        [SerializeField] private string _archetype = "All-round";

        [Header("Drives (0..1)")]
        [Tooltip("Appetite for punching other players.")]
        [Range(0f, 1f)] [SerializeField] private float _attackWill = 0.55f;

        [Tooltip("Urge to run gold home and bank it.")]
        [Range(0f, 1f)] [SerializeField] private float _saveGoldWill = 0.6f;

        [Tooltip("Interest in buying Steal and raiding enemy vaults.")]
        [Range(0f, 1f)] [SerializeField] private float _stealWill = 0.5f;

        [Tooltip("Willingness to spend on upgrades at all.")]
        [Range(0f, 1f)] [SerializeField] private float _shopWill = 0.6f;

        [Tooltip("Pull toward big piles of gold.")]
        [Range(0f, 1f)] [SerializeField] private float _greed = 0.6f;

        [Tooltip("Tendency to disengage when threatened while loaded.")]
        [Range(0f, 1f)] [SerializeField] private float _caution = 0.5f;

        [Header("Shopping taste (higher wins; 0 = never buy)")]
        [SerializeField] private float _attackUpBias = 1f;
        [SerializeField] private float _defenseUpBias = 1f;
        [SerializeField] private float _goldBagUpBias = 1f;
        [SerializeField] private float _baseCampUpBias = 0.9f;
        [SerializeField] private float _scaleUpBias = 0.6f;
        [SerializeField] private float _scaleDownBias = 0.6f;
        [SerializeField] private float _stealBias = 1.25f;

        public string DisplayName => _displayName;
        public string Archetype => _archetype;

        public NpcProfile ToProfile()
        {
            return new NpcProfile
            {
                Id = name,
                DisplayName = _displayName,
                Archetype = _archetype,
                AttackWill = _attackWill,
                SaveGoldWill = _saveGoldWill,
                StealWill = _stealWill,
                ShopWill = _shopWill,
                Greed = _greed,
                Caution = _caution,
                ShopBias = new Dictionary<ItemId, float>
                {
                    { ItemId.AttackUp, _attackUpBias },
                    { ItemId.DefenseUp, _defenseUpBias },
                    { ItemId.GoldBagUp, _goldBagUpBias },
                    { ItemId.BaseCampUp, _baseCampUpBias },
                    { ItemId.ScaleUp, _scaleUpBias },
                    { ItemId.ScaleDown, _scaleDownBias },
                    { ItemId.Steal, _stealBias },
                },
            };
        }
    }
}
