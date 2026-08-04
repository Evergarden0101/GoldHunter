using System;
using System.Collections.Generic;

namespace GoldHunter.Core.Config
{
    /// <summary>
    /// An NPC personality. Every "will" is 0..1 and blends into the utility
    /// scores in <see cref="Ai.NpcBrain"/>, so a designer can dial an archetype
    /// from the Inspector without touching AI code.
    /// </summary>
    [Serializable]
    public class NpcProfile
    {
        public string Id = "allround";
        public string DisplayName = "Pip";
        public string Archetype = "All-round";

        /// <summary>Appetite for punching other players.</summary>
        public float AttackWill = 0.55f;

        /// <summary>Urge to run gold home and bank it.</summary>
        public float SaveGoldWill = 0.6f;

        /// <summary>Interest in buying Steal and raiding enemy vaults.</summary>
        public float StealWill = 0.5f;

        /// <summary>Willingness to spend on upgrades at all.</summary>
        public float ShopWill = 0.6f;

        /// <summary>Pull toward big piles of gold.</summary>
        public float Greed = 0.6f;

        /// <summary>Tendency to disengage when threatened while loaded.</summary>
        public float Caution = 0.5f;

        /// <summary>Per-item shopping preference. Higher wins ties; 0 means never buy.</summary>
        public Dictionary<ItemId, float> ShopBias = new Dictionary<ItemId, float>();

        public float BiasFor(ItemId id)
        {
            return ShopBias.TryGetValue(id, out float w) ? w : 0.5f;
        }

        public static NpcProfile Bruiser() => new NpcProfile
        {
            Id = "bruiser", DisplayName = "Bruno", Archetype = "Bruiser",
            AttackWill = 0.9f, SaveGoldWill = 0.42f, StealWill = 0.45f, ShopWill = 0.55f,
            Greed = 0.75f, Caution = 0.2f,
            ShopBias = new Dictionary<ItemId, float>
            {
                { ItemId.AttackUp, 1.6f }, { ItemId.ScaleUp, 1.3f }, { ItemId.DefenseUp, 0.7f },
                { ItemId.GoldBagUp, 0.5f }, { ItemId.BaseCampUp, 0.3f }, { ItemId.Steal, 1.1f },
                { ItemId.ScaleDown, 0.1f },
            },
        };

        public static NpcProfile Banker() => new NpcProfile
        {
            Id = "banker", DisplayName = "Coinsworth", Archetype = "Banker",
            AttackWill = 0.2f, SaveGoldWill = 0.92f, StealWill = 0.15f, ShopWill = 0.7f,
            Greed = 0.5f, Caution = 0.85f,
            ShopBias = new Dictionary<ItemId, float>
            {
                { ItemId.GoldBagUp, 1.7f }, { ItemId.BaseCampUp, 1.5f }, { ItemId.DefenseUp, 1.1f },
                { ItemId.ScaleDown, 0.8f }, { ItemId.AttackUp, 0.3f }, { ItemId.ScaleUp, 0.2f },
                { ItemId.Steal, 0.2f },
            },
        };

        public static NpcProfile Thief() => new NpcProfile
        {
            Id = "thief", DisplayName = "Sly", Archetype = "Thief",
            AttackWill = 0.55f, SaveGoldWill = 0.45f, StealWill = 0.95f, ShopWill = 0.8f,
            Greed = 0.9f, Caution = 0.5f,
            ShopBias = new Dictionary<ItemId, float>
            {
                { ItemId.Steal, 2.2f }, { ItemId.ScaleDown, 1.2f }, { ItemId.AttackUp, 0.9f },
                { ItemId.GoldBagUp, 0.7f }, { ItemId.DefenseUp, 0.5f }, { ItemId.BaseCampUp, 0.4f },
                { ItemId.ScaleUp, 0.2f },
            },
        };

        public static NpcProfile AllRound() => new NpcProfile
        {
            Id = "allround", DisplayName = "Pip", Archetype = "All-round",
            AttackWill = 0.55f, SaveGoldWill = 0.6f, StealWill = 0.5f, ShopWill = 0.6f,
            Greed = 0.6f, Caution = 0.5f,
            ShopBias = new Dictionary<ItemId, float>
            {
                { ItemId.AttackUp, 1f }, { ItemId.DefenseUp, 1f }, { ItemId.GoldBagUp, 1f },
                { ItemId.BaseCampUp, 0.9f }, { ItemId.Steal, 1.25f }, { ItemId.ScaleUp, 0.6f },
                { ItemId.ScaleDown, 0.6f },
            },
        };

        public static List<NpcProfile> DefaultRoster() =>
            new List<NpcProfile> { Bruiser(), Banker(), Thief(), AllRound() };
    }
}
