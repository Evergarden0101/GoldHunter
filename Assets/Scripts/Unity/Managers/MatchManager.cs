using System.Collections.Generic;
using GoldHunter.Core.Ai;
using GoldHunter.Core.Config;
using GoldHunter.Core.Input;
using GoldHunter.Core.Simulation;
using GoldHunter.Unity.Actors;
using GoldHunter.Unity.Config;
using GoldHunter.Unity.Fx;
using GoldHunter.Unity.Input;
using UnityEngine;

namespace GoldHunter.Unity.Managers
{
    /// <summary>
    /// Drives a match: builds the setup from the Inspector, owns the
    /// <see cref="MatchSimulation"/>, and ticks it once per frame.
    ///
    /// The tick uses <see cref="Time.unscaledDeltaTime"/> on purpose. Hit-stop
    /// is applied inside the simulation, which slows the sim clock while
    /// presentation keeps real time — driving both off the same scaled delta is
    /// what makes impacts read as frame drops instead of punches.
    ///
    /// Drop this on one GameObject with StageManager, BaseCampManager and
    /// ShopManager beside it and press Play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("All gameplay numbers, including coin popping speed. Defaults are used when empty.")]
        [SerializeField] private GameConfigAsset _config;

        [Tooltip("How well the NPCs execute. Defaults to Normal when empty.")]
        [SerializeField] private DifficultyAsset _difficulty;

        [Header("Seats (NW, NE, SW, SE)")]
        [SerializeField]
        private SeatSetup[] _seats =
        {
            new SeatSetup { Kind = PlayerSlotKind.Human, DisplayName = "YOU" },
            new SeatSetup { Kind = PlayerSlotKind.Npc, DisplayName = "Bruno" },
            new SeatSetup { Kind = PlayerSlotKind.Npc, DisplayName = "Coinsworth" },
            new SeatSetup { Kind = PlayerSlotKind.Npc, DisplayName = "Sly" },
        };

        [Header("Match")]
        [Tooltip("Leave at 0 to pick a fresh seed each run; set it to replay a match exactly.")]
        [SerializeField] private int _seed;
        [SerializeField] private bool _startOnPlay = true;

        [Header("Scene wiring (found automatically when empty)")]
        [SerializeField] private StageManager _stageManager;
        [SerializeField] private BaseCampManager _campManager;
        [SerializeField] private ShopManager _shopManager;
        [SerializeField] private FxDirector _fxDirector;
        [SerializeField] private PlayerViewSpawner _playerSpawner;
        [SerializeField] private CoinPopperViewSpawner _popperSpawner;

        private MatchSimulation _simulation;
        private readonly List<IController> _controllers = new List<IController>();
        private bool _paused;

        public MatchSimulation Simulation => _simulation;
        public bool IsRunning => _simulation != null && _simulation.Phase != MatchPhase.Ended;
        public bool IsPaused => _paused;

        private void Awake()
        {
            if (_stageManager == null) _stageManager = GetComponentInChildren<StageManager>();
            if (_campManager == null) _campManager = GetComponentInChildren<BaseCampManager>();
            if (_shopManager == null) _shopManager = GetComponentInChildren<ShopManager>();
            if (_fxDirector == null) _fxDirector = GetComponentInChildren<FxDirector>();
            if (_playerSpawner == null) _playerSpawner = GetComponentInChildren<PlayerViewSpawner>();
            if (_popperSpawner == null) _popperSpawner = GetComponentInChildren<CoinPopperViewSpawner>();
        }

        private void Start()
        {
            if (_startOnPlay) StartMatch();
        }

        /// <summary>Builds and starts a fresh match, replacing any in progress.</summary>
        public void StartMatch()
        {
            MatchSetup setup = BuildSetup();
            _simulation = new MatchSimulation(setup, _fxDirector);
            _paused = false;

            if (_stageManager != null) _stageManager.Bind(_simulation.Stage);
            if (_campManager != null && _stageManager != null)
            {
                _campManager.Bind(_simulation.Camps, _stageManager);
            }
            if (_shopManager != null && _stageManager != null)
            {
                _shopManager.Bind(_simulation, _stageManager);
            }
            if (_fxDirector != null) _fxDirector.Bind(_simulation, _stageManager, _campManager);
            if (_playerSpawner != null) _playerSpawner.Bind(_simulation, _stageManager, _campManager);
            if (_popperSpawner != null) _popperSpawner.Bind(_simulation, _stageManager);
        }

        private MatchSetup BuildSetup()
        {
            GameConfig config = _config != null ? _config.ToConfig() : GameConfig.Default();
            DifficultySettings difficulty = _difficulty != null
                ? _difficulty.ToSettings()
                : DifficultySettings.Normal();

            var setup = new MatchSetup
            {
                Config = config,
                Difficulty = difficulty,
                Seed = _seed != 0 ? _seed : Random.Range(1, int.MaxValue),
            };

            _controllers.Clear();
            NpcProfile[] fallbackProfiles =
            {
                NpcProfile.Bruiser(), NpcProfile.Banker(), NpcProfile.Thief(), NpcProfile.AllRound(),
            };

            for (int i = 0; i < 4; i++)
            {
                SeatSetup seat = i < _seats.Length ? _seats[i] : new SeatSetup();
                IController controller = seat.Kind == PlayerSlotKind.Human
                    ? CreateHumanController(seat, i)
                    : new VirtualController(seat.DisplayName);

                _controllers.Add(controller);

                NpcProfile profile = seat.Kind == PlayerSlotKind.Npc
                    ? (seat.Profile != null ? seat.Profile.ToProfile() : fallbackProfiles[i])
                    : null;

                setup.Slots.Add(new PlayerSlot
                {
                    Kind = seat.Kind,
                    DisplayName = string.IsNullOrEmpty(seat.DisplayName)
                        ? (profile != null ? profile.DisplayName : "P" + (i + 1))
                        : seat.DisplayName,
                    Controller = controller,
                    Profile = profile,
                });
            }
            return setup;
        }

        private static IController CreateHumanController(SeatSetup seat, int index)
        {
            if (seat.UseGamepad) return new GamepadController(Mathf.Max(1, seat.JoystickNumber));

            KeyboardScheme scheme = seat.Keys;
            if (scheme == null || string.IsNullOrEmpty(scheme.Name)) scheme = DefaultSchemeFor(index);
            return new KeyboardController(scheme);
        }

        private static KeyboardScheme DefaultSchemeFor(int index)
        {
            switch (index)
            {
                case 0: return KeyboardScheme.Wasd();
                case 1: return KeyboardScheme.Ijkl();
                case 2: return KeyboardScheme.Arrows();
                default: return KeyboardScheme.Numpad();
            }
        }

        private void Update()
        {
            if (_simulation == null) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.P)) _paused = !_paused;
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) { StartMatch(); return; }
            if (_paused) return;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) dt = 1f / 60f;

            // A long stall must not teleport anyone across the arena.
            dt = Mathf.Min(dt, 1f / 20f);

            for (int i = 0; i < _controllers.Count; i++) _controllers[i].Poll(dt);
            _simulation.Tick(dt);
        }
    }

    /// <summary>One seat as configured in the Inspector.</summary>
    [System.Serializable]
    public class SeatSetup
    {
        public PlayerSlotKind Kind = PlayerSlotKind.Npc;
        public string DisplayName = "";

        [Header("Human seats")]
        public KeyboardScheme Keys = KeyboardScheme.Wasd();
        public bool UseGamepad;
        public int JoystickNumber = 1;

        [Header("NPC seats")]
        [Tooltip("Personality asset. A built-in archetype is used when empty.")]
        public NpcProfileAsset Profile;
    }
}
