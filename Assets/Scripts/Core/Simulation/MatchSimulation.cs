using System.Collections.Generic;
using System;
using GoldHunter.Core.Ai;
using GoldHunter.Core.Config;
using GoldHunter.Core.Events;
using GoldHunter.Core.Math;
using GoldHunter.Core.Navigation;
using GoldHunter.Core.Services;

namespace GoldHunter.Core.Simulation
{
    /// <summary>Match length and the end-game rush. Editable in the Inspector.</summary>
    [Serializable]
    public class MatchSettings
    {
        /// <summary>Match duration in seconds. 150 = the 2.5 minute round.</summary>
        public float Duration = 150f;

        /// <summary>Seconds of "3 - 2 - 1 - GO" before the clock starts.</summary>
        public float CountdownSeconds = 3f;

        /// <summary>Seconds remaining when the Gold Rush begins.</summary>
        public float RushAtSecondsLeft = 25f;

        /// <summary>Popper output multiplier during the rush.</summary>
        public float RushPopperMultiplier = 2.5f;

        /// <summary>One-off gold dumped into the motherlode when the rush starts.</summary>
        public float RushBurstGold = 60f;
    }

    /// <summary>
    /// The whole game, with no engine attached.
    ///
    /// Call <see cref="Tick"/> once per frame with the real (unscaled) delta.
    /// It applies hit-stop to derive the simulation delta internally, so the
    /// presentation layer must keep animating on the real delta it passed in —
    /// feeding both the same value is what makes impacts look like frame drops
    /// instead of punches.
    /// </summary>
    public sealed class MatchSimulation
    {
        private readonly List<PlayerState> _players = new List<PlayerState>();
        private readonly List<GoldPickup> _pickups = new List<GoldPickup>();
        private readonly List<Vec2> _playerPositions = new List<Vec2>();
        private readonly List<NpcBrain> _brains = new List<NpcBrain>();
        private readonly DeterministicRng _rng;
        private float _depositSoundCarry;

        public GameConfig Config { get; }
        public DifficultySettings Difficulty { get; }
        public ISimulationListener Listener { get; }

        public StageService Stage { get; }
        public ShoppingService Shopping { get; }
        public BaseCampService Camps { get; }
        public CombatResolver Combat { get; }
        public HitStopClock HitStop { get; } = new HitStopClock();

        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>Active NPC brains, for debug path drawing and the test harness.</summary>
        public IReadOnlyList<NpcBrain> Brains => _brains;
        public IReadOnlyList<GoldPickup> Pickups => _pickups;
        public IReadOnlyList<CoinPopper> Poppers => Stage.Poppers;
        public IReadOnlyList<Shop> Shops => Stage.Shops;

        public MatchPhase Phase { get; private set; } = MatchPhase.Countdown;
        public float ElapsedTime { get; private set; }
        public float CountdownRemaining { get; private set; }
        public bool RushStarted { get; private set; }

        /// <summary>
        /// Gold that evaporated on the floor because nobody picked it up in time.
        /// Poppers are the only source of gold; shop spending and this are the
        /// only two sinks, which is what the conservation test checks against.
        /// </summary>
        public float GoldExpired { get; private set; }
        public IReadOnlyList<MatchResultRow> Results { get; private set; }

        public float TimeRemaining => GhMath.Max(0f, Config.Match.Duration - ElapsedTime);
        public bool IsRushing => Phase == MatchPhase.Playing && TimeRemaining <= Config.Match.RushAtSecondsLeft;
        public int LeaderIndex => Camps.LeaderIndex();

        public MatchSimulation(MatchSetup setup, ISimulationListener listener = null)
        {
            Config = setup.Config;
            Difficulty = setup.Difficulty;
            Listener = listener ?? NullSimulationListener.Instance;
            _rng = new DeterministicRng(setup.Seed);
            CountdownRemaining = Config.Match.CountdownSeconds + 0.999f;

            List<BaseCamp> camps = ArenaBuilder.BuildCamps(Config);
            List<CoinPopper> poppers = ArenaBuilder.BuildPoppers(Config);
            List<Shop> shops = ArenaBuilder.BuildShops(Config);
            List<RockObstacle> rocks = ArenaBuilder.BuildRocks(Config);

            Stage = new StageService(Config, poppers, shops, camps, rocks);
            Shopping = new ShoppingService(Config);

            BuildPlayers(setup, camps);

            Camps = new BaseCampService(Config, camps, _players);
            Combat = new CombatResolver(Config, Camps, Listener, _rng);

            BuildBrains(setup);

            Listener.OnAnnouncement(AnnouncementKind.GetReady, "GET READY");
        }

        private void BuildPlayers(MatchSetup setup, List<BaseCamp> camps)
        {
            for (int i = 0; i < 4; i++)
            {
                PlayerSlot slot = i < setup.Slots.Count ? setup.Slots[i] : new PlayerSlot();
                ArenaBuilder.SpawnFor(Config, i, out Vec2 position, out float facing);

                var player = new PlayerState(
                    i,
                    string.IsNullOrEmpty(slot.DisplayName) ? "P" + (i + 1) : slot.DisplayName,
                    slot.Kind == PlayerSlotKind.Human,
                    slot.Controller,
                    camps[i],
                    position,
                    facing,
                    Config)
                {
                    Profile = slot.Profile,
                };
                _players.Add(player);
                _playerPositions.Add(position);
            }
        }

        private void BuildBrains(MatchSetup setup)
        {
            var pathfinder = new AStarPathfinder(Stage.NavGrid, Config.Navigation);
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerSlot slot = i < setup.Slots.Count ? setup.Slots[i] : null;
                if (slot == null || slot.Kind != PlayerSlotKind.Npc) continue;

                NpcProfile profile = slot.Profile ?? NpcProfile.AllRound();
                _players[i].Profile = profile;
                var follower = new PathFollower(pathfinder, Stage.NavGrid, Config.Navigation);
                _brains.Add(new NpcBrain(_players[i], profile, Difficulty, this, follower, setup.Seed + i * 77));
            }
        }

        /* ----------------------------------------------------------------- tick */

        /// <param name="realDeltaTime">Unscaled wall-clock seconds since the last frame.</param>
        public void Tick(float realDeltaTime)
        {
            float dt = HitStop.ScaleDelta(realDeltaTime);

            if (Phase == MatchPhase.Countdown)
            {
                TickCountdown(realDeltaTime, dt);
                return;
            }

            if (Phase == MatchPhase.Ended)
            {
                TickIdleScenery(dt);
                return;
            }

            ElapsedTime += dt;

            if (!RushStarted && IsRushing)
            {
                RushStarted = true;
                Stage.Poppers[0].Inject(Config.Match.RushBurstGold);
                Stage.Poppers[0].AddShake(1.2f);
                Listener.OnAnnouncement(AnnouncementKind.GoldRush, "GOLD RUSH!");
                Listener.OnTicker("Poppers overflowing — final seconds!", -1);
            }

            if (ElapsedTime >= Config.Match.Duration)
            {
                Finish();
                return;
            }

            float rateMultiplier = IsRushing ? Config.Match.RushPopperMultiplier : 1f;
            for (int i = 0; i < Stage.Poppers.Count; i++)
            {
                if (Stage.Poppers[i].Tick(dt, rateMultiplier)) Listener.OnPopperGenerated(Stage.Poppers[i]);
            }

            Camps.Tick(dt);
            for (int i = 0; i < Stage.Shops.Count; i++) Stage.Shops[i].Customers.Clear();

            // Brains write into their controllers; the players then read those
            // controllers exactly as a human's keyboard would be read.
            for (int i = 0; i < _brains.Count; i++) _brains[i].Tick(dt);
            for (int i = 0; i < _players.Count; i++) _players[i].Tick(dt, this);

            ResolveCollisions();
            ResolveInteractions(dt);
            Combat.ResolveActivePunches(_players, Camps.Camps, Stage.Poppers, _pickups);
            TickPickups(dt);
        }

        private void TickCountdown(float realDt, float dt)
        {
            int before = (int)System.Math.Ceiling(CountdownRemaining);
            CountdownRemaining -= realDt;
            int after = (int)System.Math.Ceiling(CountdownRemaining);

            if (after != before && after > 0)
            {
                Listener.OnAnnouncement(AnnouncementKind.CountdownTick, after.ToString());
            }

            if (CountdownRemaining <= 0f)
            {
                Phase = MatchPhase.Playing;
                Listener.OnPhaseChanged(Phase);
                Listener.OnAnnouncement(AnnouncementKind.Go, "GO!");
            }

            // Poppers hold station during the countdown so the opening is fair.
            for (int i = 0; i < Stage.Poppers.Count; i++) Stage.Poppers[i].Tick(dt, 0f);
            Camps.Tick(dt);
        }

        private void TickIdleScenery(float dt)
        {
            for (int i = 0; i < Stage.Poppers.Count; i++) Stage.Poppers[i].Tick(dt, 0f);
            Camps.Tick(dt);
        }

        private void Finish()
        {
            Phase = MatchPhase.Ended;
            ElapsedTime = Config.Match.Duration;

            var rows = new List<MatchResultRow>();
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                BaseCamp camp = Camps.CampOf(i);
                float bonus = camp.Vault * p.EndBonusRate;
                rows.Add(new MatchResultRow
                {
                    PlayerIndex = i,
                    Name = p.Name,
                    IsHuman = p.IsHuman,
                    Profile = p.Profile,
                    Vault = camp.Vault,
                    Bonus = bonus,
                    Total = Camps.FinalScore(p),
                    Carried = p.Bag,
                    Stats = p.Stats,
                    ScaleLevel = p.ScaleLevel,
                    Purchases = p.Purchases,
                });
            }
            rows.Sort((a, b) => b.Total.CompareTo(a.Total));
            for (int i = 0; i < rows.Count; i++) rows[i].Place = i + 1;

            Results = rows;
            Listener.OnPhaseChanged(Phase);
            Listener.OnAnnouncement(AnnouncementKind.MatchOver, rows[0].Name + " WINS");
            Listener.OnMatchEnded(rows);
        }

        /* ---------------------------------------------------------- collisions */

        private void ResolveCollisions()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (Stage.ResolveBlockers(ref p.Position, ref p.Velocity, p.Radius, out Obstacle hit)
                    && hit.Kind == ObstacleKind.CoinPopper && p.DashTimer > 0f)
                {
                    Stage.Poppers[hit.SourceIndex].AddShake(0.25f);
                }
                Stage.ClampToArena(ref p.Position, ref p.Velocity, p.Radius);
            }

            // Players push each other apart, heavier (larger) players push harder.
            for (int i = 0; i < _players.Count; i++)
            {
                for (int j = i + 1; j < _players.Count; j++)
                {
                    PlayerState a = _players[i];
                    PlayerState b = _players[j];
                    if (!Geometry.CirclePush(a.Position, a.Radius, b.Position, b.Radius, out Vec2 push)) continue;

                    float totalMass = a.Scale + b.Scale;
                    a.Position += push * (b.Scale / totalMass);
                    b.Position -= push * (a.Scale / totalMass);
                }
            }

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                Stage.ClampToArena(ref p.Position, ref p.Velocity, p.Radius);
                _playerPositions[i] = p.Position;
            }
        }

        /* -------------------------------------------------------- interactions */

        private void ResolveInteractions(float dt)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState player = _players[i];

                // --- mining ---
                for (int p = 0; p < Stage.Poppers.Count; p++)
                {
                    CoinPopper popper = Stage.Poppers[p];
                    if (!Stage.CanHarvestAt(player.Position, player.Radius, p)) continue;

                    float mined = popper.Harvest(player.BagSpace, dt);
                    if (mined <= 0f) continue;

                    player.Bag += mined;
                    player.Stats.Mined += mined;
                    player.MarkMining();
                    Listener.OnMined(new MinedEvent { Player = player, Popper = popper, Amount = mined });
                }

                // --- banking (stepping into your own camp deposits automatically) ---
                if (player.Bag > 0f && Stage.CanDepositAt(player.Position, player.Radius, player.Home))
                {
                    float banked = Camps.Deposit(player, dt);
                    if (banked > 0f)
                    {
                        _depositSoundCarry += banked;
                        bool emptied = player.Bag <= 0f;
                        if (_depositSoundCarry > 14f || emptied)
                        {
                            _depositSoundCarry = 0f;
                            Listener.OnDeposited(new DepositEvent
                            {
                                Player = player, Camp = player.Home, Amount = banked, BagEmptied = emptied,
                            });
                        }
                    }
                }

                // --- shops ---
                Shop shop = Stage.FindShopInRange(player.Position, player.Radius);
                if (shop != player.CurrentShop)
                {
                    if (player.CurrentShop != null) Listener.OnShopExited(player, player.CurrentShop);
                    player.BuyHold = 0f;
                    player.CurrentShop = shop;
                    if (shop != null) Listener.OnShopEntered(player, shop);
                }
                if (shop != null) shop.Customers.Add(player.Index);
            }
        }

        private void TickPickups(float dt)
        {
            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                GoldPickup pickup = _pickups[i];
                pickup.Tick(dt, _players);
                if (pickup.IsDead)
                {
                    GoldExpired += pickup.Amount;
                    _pickups.RemoveAt(i);
                    continue;
                }

                for (int p = 0; p < _players.Count; p++)
                {
                    PlayerState player = _players[p];
                    if (player.BagSpace <= 0f) continue;
                    if (pickup.OwnerLockout > 0f && player.Index == pickup.OwnerIndex) continue;

                    float reach = player.Radius + Config.Pickup.Radius + 0.3f;
                    if (Vec2.Distance(player.Position, pickup.Position) > reach) continue;

                    // Collect only what fits. Removing the whole blob when the bag
                    // is nearly full would silently destroy the remainder.
                    float got = player.AddGold(pickup.Amount);
                    if (got <= 0f) continue;

                    pickup.Amount -= got;
                    Listener.OnPickupCollected(new PickupCollectedEvent
                    {
                        Player = player, Position = pickup.Position, Amount = got,
                    });

                    if (pickup.Amount <= 0.001f)
                    {
                        _pickups.RemoveAt(i);
                        break;
                    }
                }
            }

            int overflow = _pickups.Count - Config.Pickup.MaxActive;
            for (int i = 0; i < overflow; i++) GoldExpired += _pickups[i].Amount;
            if (overflow > 0) _pickups.RemoveRange(0, overflow);
        }

        /* ------------------------------------------------------------ purchases */

        /// <summary>
        /// Buys the item for a player standing in a shop. Rejections are reported
        /// through the listener so the UI can explain itself.
        /// </summary>
        public bool TryBuy(PlayerState player, ItemId itemId)
        {
            ShopItemDefinition def = Config.Catalogue.Find(itemId);
            if (def == null) return false;

            if (player.CurrentShop == null)
            {
                Listener.OnPurchaseRejected(new PurchaseRejectedEvent
                {
                    Buyer = player, Item = def, Reason = PurchaseRejection.NotAtShop,
                });
                return false;
            }

            if (Shopping.IsMaxed(player, itemId))
            {
                Listener.OnPurchaseRejected(new PurchaseRejectedEvent
                {
                    Buyer = player, Item = def, Reason = PurchaseRejection.AlreadyMaxLevel,
                });
                return false;
            }

            if (!Shopping.TryPurchase(player, itemId, out int price, out float fromBag, out float fromVault))
            {
                Listener.OnPurchaseRejected(new PurchaseRejectedEvent
                {
                    Buyer = player,
                    Item = def,
                    Reason = PurchaseRejection.NotEnoughGold,
                    Shortfall = Shopping.PriceOf(player, itemId) - Shopping.Funds(player),
                });
                return false;
            }

            Listener.OnPurchase(new PurchaseEvent
            {
                Buyer = player, Item = def, Price = price,
                FromBag = fromBag, FromVault = fromVault, NewLevel = player.GetLevel(itemId),
            });

            if (itemId == ItemId.Steal)
            {
                Listener.OnAnnouncement(AnnouncementKind.StealUnlocked, player.Name + " CAN RAID VAULTS");
                Listener.OnTicker(player.Name + " bought STEAL — vaults are no longer safe", player.Index);
                for (int i = 0; i < Camps.Camps.Count; i++)
                {
                    if (Camps.Camps[i].OwnerIndex != player.Index) Camps.Camps[i].RaiseAlarm(1.2f);
                }
            }
            return true;
        }

        /* ---------------------------------------------------------------- utils */

        public IReadOnlyList<Vec2> PlayerPositions => _playerPositions;

        public int CountPlayersNear(Vec2 point, float radius, PlayerState exclude)
        {
            int count = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i] == exclude) continue;
                if (Vec2.Distance(_players[i].Position, point) <= radius) count++;
            }
            return count;
        }

        /// <summary>
        /// Total gold anywhere in the world. Poppers are the only source and shop
        /// spending the only sink, so between purchases this must stay constant —
        /// the headless tests assert exactly that.
        /// </summary>
        public float TotalGoldInPlay()
        {
            float total = 0f;
            for (int i = 0; i < Stage.Poppers.Count; i++) total += Stage.Poppers[i].Gold;
            for (int i = 0; i < _players.Count; i++) total += _players[i].Bag;
            for (int i = 0; i < Camps.Camps.Count; i++) total += Camps.Camps[i].Vault;
            for (int i = 0; i < _pickups.Count; i++) total += _pickups[i].Amount;
            return total;
        }
    }
}
