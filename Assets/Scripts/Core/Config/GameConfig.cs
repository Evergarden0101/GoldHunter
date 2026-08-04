using System;

namespace GoldHunter.Core.Config
{
    /// <summary>
    /// Every gameplay number in one object. The simulation reads only from here,
    /// and the Unity layer hands it over from a ScriptableObject, so balance is
    /// entirely editor-driven.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public MatchSettings Match = new MatchSettings();
        public ArenaSettings Arena = new ArenaSettings();
        public PlayerSettings Player = new PlayerSettings();
        public CombatSettings Combat = new CombatSettings();
        public BaseCampSettings Camp = new BaseCampSettings();
        public ShopSettings Shop = new ShopSettings();
        public PickupSettings Pickup = new PickupSettings();
        public UpgradeSettings Upgrade = new UpgradeSettings();
        public NavigationSettings Navigation = new NavigationSettings();

        public CoinPopperSettings Motherlode = CoinPopperSettings.Motherlode();
        public CoinPopperSettings SmallPopper = CoinPopperSettings.Small();

        public ShopCatalogue Catalogue = ShopCatalogue.Default();

        public CoinPopperSettings ForKind(PopperKind kind)
        {
            return kind == PopperKind.Motherlode ? Motherlode : SmallPopper;
        }

        public static GameConfig Default() => new GameConfig();
    }
}
