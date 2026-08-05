using System;
using GoldHunter.Core.Navigation;
using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;

namespace GoldHunter.Core.Config
{
    /// <summary>
    /// Every gameplay number in one object. The simulation reads only from here,
    /// and the Unity layer hands it over from a ScriptableObject, so balance is
    /// entirely editor-driven.
    ///
    /// Each settings class lives in the same file as the logic that consumes it
    /// — <c>CoinPopperSettings</c> beside <c>CoinPopper</c>, <c>CombatSettings</c>
    /// beside <c>CombatResolver</c> — so a knob and its effect are always read
    /// together. This type is only the aggregate root that hands them out.
    ///
    /// Why the tunables are fields and not properties: Unity serialises public
    /// *fields* of a <c>[Serializable]</c> class, and never properties. The
    /// modern <c>[field: SerializeField]</c> idiom is not available here because
    /// GoldHunter.Core sets <c>noEngineReferences</c> and so cannot reference
    /// <c>UnityEngine.SerializeField</c> at all. Turning these into
    /// auto-properties would compile and then silently disappear from the
    /// Inspector. Everything *derived* from them is a property — see
    /// <see cref="Services.ArenaSettings.DiagonalLimit"/>,
    /// <see cref="Simulation.CombatSettings.ChargeSpan"/> and
    /// <see cref="Simulation.CoinPopperSettings.GoldPerSecond"/>.
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
