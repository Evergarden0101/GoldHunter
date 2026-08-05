using GoldHunter.Core.Config;
using GoldHunter.Core.Navigation;
using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;
using UnityEngine;

namespace GoldHunter.Unity.Config
{
    /// <summary>
    /// Every gameplay number, as an asset you can tune in the Inspector without
    /// touching code or entering play mode.
    ///
    /// Coin popping speed lives under Motherlode / Small Popper -> Gold Per
    /// Minute. Nothing in the simulation hard-codes a gameplay value, so this
    /// asset is the single place balance changes belong.
    ///
    /// Create via: Assets > Create > GoldHunter > Game Config
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "GoldHunter/Game Config", order = 0)]
    public sealed class GameConfigAsset : ScriptableObject
    {
        [Header("Match")]
        [Tooltip("Match length in seconds. 150 = the 2.5 minute round.")]
        [SerializeField] private MatchSettings _match = new MatchSettings();

        [Header("Arena")]
        [SerializeField] private ArenaSettings _arena = new ArenaSettings();

        [Header("Player")]
        [SerializeField] private PlayerSettings _player = new PlayerSettings();

        [Header("Combat")]
        [SerializeField] private CombatSettings _combat = new CombatSettings();

        [Header("Coin poppers — Gold Per Minute is the popping speed")]
        [Tooltip("The big centre machine: starts at 50g and pumps 200g/min by default.")]
        [SerializeField] private CoinPopperSettings _motherlode = CoinPopperSettings.Motherlode();

        [Tooltip("The two flanking machines: start at 20g and pump 80g/min by default.")]
        [SerializeField] private CoinPopperSettings _smallPopper = CoinPopperSettings.Small();

        [Header("Base camps & shops")]
        [SerializeField] private BaseCampSettings _camp = new BaseCampSettings();
        [SerializeField] private ShopSettings _shop = new ShopSettings();

        [Header("Loose gold")]
        [SerializeField] private PickupSettings _pickup = new PickupSettings();

        [Header("Upgrade effects")]
        [SerializeField] private UpgradeSettings _upgrade = new UpgradeSettings();

        [Header("NPC navigation")]
        [SerializeField] private NavigationSettings _navigation = new NavigationSettings();

        [Header("Shop catalogue")]
        [SerializeField] private ShopCatalogue _catalogue = ShopCatalogue.Default();

        /// <summary>Builds the plain-C# config the simulation consumes.</summary>
        public GameConfig ToConfig()
        {
            return new GameConfig
            {
                Match = _match,
                Arena = _arena,
                Player = _player,
                Combat = _combat,
                Motherlode = _motherlode,
                SmallPopper = _smallPopper,
                Camp = _camp,
                Shop = _shop,
                Pickup = _pickup,
                Upgrade = _upgrade,
                Navigation = _navigation,
                Catalogue = _catalogue != null && _catalogue.Count > 0 ? _catalogue : ShopCatalogue.Default(),
            };
        }

        private void OnValidate()
        {
            // Guard the values that would break the simulation outright.
            if (_match.Duration < 5f) _match.Duration = 5f;
            if (_arena.Half < 10f) _arena.Half = 10f;
            if (_navigation.CellSize < 0.4f) _navigation.CellSize = 0.4f;
            if (_player.BagCapacity < 1f) _player.BagCapacity = 1f;
            if (_motherlode.Capacity < _motherlode.StartingGold) _motherlode.Capacity = _motherlode.StartingGold;
            if (_smallPopper.Capacity < _smallPopper.StartingGold) _smallPopper.Capacity = _smallPopper.StartingGold;
        }
    }
}
