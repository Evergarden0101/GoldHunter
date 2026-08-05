using System.Collections.Generic;
using GoldHunter.Core.Ai;
using GoldHunter.Core.Config;
using GoldHunter.Core.Input;
using GoldHunter.Core.Math;
using GoldHunter.Core.Navigation;
using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;

namespace GoldHunter.CoreTests
{
    /// <summary>
    /// Focused checks on the rules that are easy to break silently and hard to
    /// notice by playing: the arena's fairness, the octagon clamp, tap-vs-hold
    /// detection, pathing around blockers, and the shop's funding order.
    /// </summary>
    public static class UnitChecks
    {
        public static List<string> RunAll()
        {
            var failures = new List<string>();
            ArenaIsSymmetric(failures);
            ClampKeepsBodiesInside(failures);
            TapAndHoldAreDistinguished(failures);
            PathingRoutesAroundBlockers(failures);
            ShopBillsBagBeforeVault(failures);
            ShopRefusesWhatCannotBeAfforded(failures);
            HitStopScalesOnlyTheSimClock(failures);
            StageAnswersInteractableQueries(failures);
            return failures;
        }

        private static void Check(List<string> failures, bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        /// <summary>Every seat must be an equal distance from the gold and the shops.</summary>
        private static void ArenaIsSymmetric(List<string> failures)
        {
            GameConfig config = GameConfig.Default();
            List<BaseCamp> camps = ArenaBuilder.BuildCamps(config);
            List<CoinPopper> poppers = ArenaBuilder.BuildPoppers(config);
            List<Shop> shops = ArenaBuilder.BuildShops(config);

            float firstToMotherlode = Vec2.Distance(camps[0].Position, poppers[0].Position);
            for (int i = 0; i < camps.Count; i++)
            {
                float d = Vec2.Distance(camps[i].Position, poppers[0].Position);
                Check(failures, GhMath.Abs(d - config.Arena.CampRadius) < 0.01f,
                    $"camp {i} is {d:0.00}m from the motherlode, expected {config.Arena.CampRadius}");
                Check(failures, GhMath.Abs(d - firstToMotherlode) < 0.01f,
                    "camps are not all the same distance from the motherlode");

                float nearestPopper = float.MaxValue;
                for (int p = 1; p < poppers.Count; p++)
                {
                    nearestPopper = GhMath.Min(nearestPopper,
                        Vec2.Distance(camps[i].Position, poppers[p].Position));
                }
                float nearestShop = float.MaxValue;
                for (int s = 0; s < shops.Count; s++)
                {
                    nearestShop = GhMath.Min(nearestShop, Vec2.Distance(camps[i].Position, shops[s].Position));
                }
                Check(failures, GhMath.Abs(nearestPopper - nearestShop) < 0.05f,
                    $"camp {i} is not equidistant from its small popper ({nearestPopper:0.00}m) " +
                    $"and its shop ({nearestShop:0.00}m)");
            }
        }

        /// <summary>The octagon clamp must pull a body back inside from any direction.</summary>
        private static void ClampKeepsBodiesInside(List<string> failures)
        {
            var sim = BuildSimulation(out _);
            StageService stage = sim.Stage;
            const float radius = 1.2f;

            var probes = new[]
            {
                new Vec2(999f, 0f), new Vec2(-999f, 0f), new Vec2(0f, 999f), new Vec2(0f, -999f),
                new Vec2(400f, 400f), new Vec2(-400f, 400f), new Vec2(34f, 34f), new Vec2(-34f, -34f),
            };

            foreach (Vec2 probe in probes)
            {
                Vec2 position = probe;
                Vec2 velocity = probe.Normalized * 10f;
                stage.ClampToArena(ref position, ref velocity, radius);
                Check(failures, stage.IsInsideArena(position, radius * 0.5f),
                    $"clamp left a body outside the arena (from {probe} to {position})");
            }
        }

        /// <summary>
        /// A jab and a smash differ only by how long the button was held, so the
        /// release must carry that duration. This is the bug the browser build
        /// shipped with: a tap consumed inside one frame vanished entirely.
        /// </summary>
        private static void TapAndHoldAreDistinguished(List<string> failures)
        {
            var button = new ButtonState();
            const float dt = 1f / 60f;

            button.Update(true, dt);
            button.Update(false, dt);
            Check(failures, button.WasReleased, "a one-frame tap produced no release");
            Check(failures, button.ReleaseHoldTime <= 0.02f,
                $"a tap reported a hold of {button.ReleaseHoldTime:0.000}s");

            var held = new ButtonState();
            for (int i = 0; i < 60; i++) held.Update(true, dt);
            held.Update(false, dt);
            Check(failures, held.ReleaseHoldTime > 0.9f,
                $"a one-second hold reported {held.ReleaseHoldTime:0.000}s");
        }

        /// <summary>A route between two camps must exist and must not clip a rock.</summary>
        private static void PathingRoutesAroundBlockers(List<string> failures)
        {
            var sim = BuildSimulation(out _);
            var pathfinder = new AStarPathfinder(sim.Stage.NavGrid, sim.Config.Navigation);
            var path = new List<Vec2>();

            Vec2 from = sim.Camps.CampOf(0).Position;
            Vec2 to = sim.Camps.CampOf(3).Position;
            Check(failures, pathfinder.TryFindPath(from, to, path), "no path between opposite camps");
            Check(failures, path.Count > 0, "path was empty");

            Vec2 cursor = from;
            for (int i = 0; i < path.Count; i++)
            {
                foreach (Obstacle o in sim.Stage.Obstacles)
                {
                    // Waypoints are string-pulled, so legs must clear the blockers.
                    Check(failures, !Geometry.SegmentHitsCircle(cursor, path[i], o.Position, o.Radius * 0.9f),
                        "a path leg cut straight through a blocker");
                }
                cursor = path[i];
            }
        }

        /// <summary>Purchases spend the bag first and only then dip into the vault.</summary>
        private static void ShopBillsBagBeforeVault(List<string> failures)
        {
            var sim = BuildSimulation(out _);
            PlayerState player = sim.Players[0];
            int price = sim.Shopping.PriceOf(player, ItemId.AttackUp);

            // Case 1: the bag covers it entirely.
            player.Bag = price + 10f;
            float vaultBefore = player.Home.Vault;
            Check(failures, sim.Shopping.TryPurchase(player, ItemId.AttackUp,
                    out int paid, out float fromBag, out float fromVault),
                "an affordable purchase was refused");
            Check(failures, paid == price, $"charged {paid} for a {price}g item");
            Check(failures, GhMath.Abs(fromBag - price) < 0.01f, "the bag did not pay first");
            Check(failures, fromVault == 0f, "the vault paid while the bag still had gold");
            Check(failures, GhMath.Abs(player.Home.Vault - vaultBefore) < 0.01f, "the vault was touched");
            Check(failures, player.GetLevel(ItemId.AttackUp) == 1, "the upgrade level did not rise");

            // Case 2: the bag is short, so the vault covers the remainder.
            int nextPrice = sim.Shopping.PriceOf(player, ItemId.AttackUp);
            player.Bag = 5f;
            player.Home.Deposit(nextPrice);
            float vaultBeforeSplit = player.Home.Vault;
            Check(failures, sim.Shopping.TryPurchase(player, ItemId.AttackUp,
                    out int paid2, out float bag2, out float vault2),
                "a split-funded purchase was refused");
            Check(failures, GhMath.Abs(bag2 - 5f) < 0.01f, "the bag was not drained first on a split payment");
            Check(failures, GhMath.Abs(bag2 + vault2 - paid2) < 0.01f, "the split payment did not add up");
            Check(failures, GhMath.Abs(player.Home.Vault - (vaultBeforeSplit - vault2)) < 0.01f,
                "the vault was not debited by the remainder");
            Check(failures, player.Bag == 0f, "the bag was not emptied on a split payment");
        }

        private static void ShopRefusesWhatCannotBeAfforded(List<string> failures)
        {
            var sim = BuildSimulation(out _);
            PlayerState player = sim.Players[1];
            player.Bag = 0f;

            Check(failures, !sim.Shopping.CanBuy(player, ItemId.Steal),
                "a broke player was allowed to buy Steal");
            Check(failures, !sim.Shopping.TryPurchase(player, ItemId.Steal, out _, out _, out _),
                "an unaffordable purchase went through");
            Check(failures, !player.CanSteal, "Steal was granted without payment");

            // Maxed items stay refused even when rich.
            player.Home.Deposit(10000f);
            Check(failures, sim.Shopping.TryPurchase(player, ItemId.Steal, out _, out _, out _),
                "a funded Steal purchase was refused");
            Check(failures, player.CanSteal, "Steal did not unlock raiding");
            Check(failures, sim.Shopping.IsMaxed(player, ItemId.Steal), "Steal did not report as maxed");
            Check(failures, !sim.Shopping.TryPurchase(player, ItemId.Steal, out _, out _, out _),
                "Steal was sold twice");
        }

        /// <summary>
        /// Hit-stop must slow the simulation while presentation keeps real time.
        /// Feeding both the same delta is what makes a punch look like a stutter.
        /// </summary>
        private static void HitStopScalesOnlyTheSimClock(List<string> failures)
        {
            var clock = new HitStopClock();
            const float realDt = 1f / 60f;

            Check(failures, GhMath.Abs(clock.ScaleDelta(realDt) - realDt) < 1e-6f,
                "the clock scaled time while no freeze was active");

            clock.Request(0.1f);
            float frozen = clock.ScaleDelta(realDt);
            Check(failures, frozen < realDt * 0.2f, $"a freeze barely slowed the sim ({frozen:0.0000}s)");
            Check(failures, frozen > 0f, "a freeze stopped the sim dead, which reads as a hang");

            // Longer requests win; they never stack.
            clock.Request(0.05f);
            Check(failures, clock.Remaining > 0.05f, "a shorter freeze request shortened an active freeze");

            for (int i = 0; i < 60; i++) clock.ScaleDelta(realDt);
            Check(failures, !clock.IsFrozen, "the freeze never expired");
        }

        /// <summary>The stage must answer "what is here?" consistently for gameplay and UI.</summary>
        private static void StageAnswersInteractableQueries(List<string> failures)
        {
            var sim = BuildSimulation(out _);
            StageService stage = sim.Stage;

            InteractableHit atMotherlode = stage.QueryInteractable(stage.Poppers[0].Position, 1.2f, 0);
            Check(failures, atMotherlode.Kind == InteractableKind.CoinPopper,
                $"standing on the motherlode reported {atMotherlode.Kind}");

            InteractableHit atOwnCamp = stage.QueryInteractable(sim.Camps.CampOf(0).Position, 1.2f, 0);
            Check(failures, atOwnCamp.Kind == InteractableKind.OwnBaseCamp,
                $"standing in your own camp reported {atOwnCamp.Kind}");

            InteractableHit atEnemyCamp = stage.QueryInteractable(sim.Camps.CampOf(1).Position, 1.2f, 0);
            Check(failures, atEnemyCamp.Kind == InteractableKind.EnemyBaseCamp,
                $"standing in a rival camp reported {atEnemyCamp.Kind}");

            InteractableHit atShop = stage.QueryInteractable(stage.Shops[0].Position, 1.2f, 0);
            Check(failures, atShop.Kind == InteractableKind.Shop,
                $"standing at a shop reported {atShop.Kind}");

            // Camps must stay walkable or nobody could ever deposit.
            Check(failures, stage.IsWalkable(sim.Camps.CampOf(0).Position, 1.2f),
                "a base camp is solid — its owner could never step in to bank");

            // Poppers and shops must be solid.
            Check(failures, !stage.IsWalkable(stage.Poppers[0].Position, 1.2f),
                "the motherlode is not solid");
            Check(failures, !stage.IsWalkable(stage.Shops[0].Position, 1.2f),
                "a shop is not solid");
        }

        private static MatchSimulation BuildSimulation(out MatchSetup setup)
        {
            setup = new MatchSetup { Config = GameConfig.Default(), Seed = 4242 };
            for (int i = 0; i < 4; i++)
            {
                setup.Slots.Add(new PlayerSlot
                {
                    Kind = PlayerSlotKind.Npc,
                    DisplayName = "P" + (i + 1),
                    Profile = NpcProfile.AllRound(),
                    Controller = new VirtualController("test"),
                });
            }
            return new MatchSimulation(setup);
        }
    }
}
