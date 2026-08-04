using GoldHunter.Core.Simulation;
using GoldHunter.Unity.Managers;
using UnityEngine;

namespace GoldHunter.Unity.UI
{
    /// <summary>
    /// The in-match HUD: clock, per-seat cards and the end-of-match standings.
    ///
    /// Drawn with IMGUI on purpose — it needs no uGUI/TextMeshPro packages, no
    /// canvas and no prefab wiring, so the project runs in a bare scene.
    /// Replace it with a uGUI canvas when you want art direction.
    ///
    /// Each seat's card sits in the screen corner matching its camp
    /// (NW/NE/SW/SE), so nobody has to hunt for their own numbers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private MatchManager _match;
        [SerializeField] private BaseCampManager _camps;
        [SerializeField] private bool _showControlHints = true;

        private GUIStyle _clockStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _vaultStyle;
        private GUIStyle _smallStyle;

        /// <summary>
        /// Resolved lazily, not in Awake: the bootstrap adds MatchManager after
        /// this component, so an Awake-time lookup would always find nothing.
        /// </summary>
        private void ResolveDependencies()
        {
            if (_match == null) _match = FindObjectOfType<MatchManager>();
            if (_camps == null) _camps = FindObjectOfType<BaseCampManager>();
        }

        private void OnGUI()
        {
            ResolveDependencies();
            MatchSimulation sim = _match != null ? _match.Simulation : null;
            if (sim == null) return;

            EnsureStyles();
            DrawClock(sim);
            DrawSeatCards(sim);

            if (sim.Phase == MatchPhase.Ended) DrawResults(sim);
            else if (_showControlHints) DrawHints(sim);

            if (_match.IsPaused)
            {
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 50), "PAUSED", _clockStyle);
            }
        }

        private void DrawClock(MatchSimulation sim)
        {
            float remaining = sim.Phase == MatchPhase.Countdown
                ? sim.Config.Match.Duration
                : sim.TimeRemaining;

            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);

            _clockStyle.normal.textColor = sim.IsRushing
                ? new Color(1f, 0.6f, 0.36f)
                : (remaining < 30f ? new Color(1f, 0.82f, 0.25f) : Color.white);

            GUI.Label(new Rect(0, 8, Screen.width, 44), $"{minutes}:{seconds:00}", _clockStyle);
            GUI.Label(new Rect(0, 50, Screen.width, 20),
                sim.IsRushing ? "GOLD RUSH" : "TIME LEFT", _smallStyle);
        }

        private void DrawSeatCards(MatchSimulation sim)
        {
            const float width = 220f;
            const float height = 74f;
            const float pad = 12f;

            for (int i = 0; i < sim.Players.Count; i++)
            {
                PlayerState player = sim.Players[i];
                BaseCamp camp = sim.Camps.CampOf(i);

                // Corner matches the camp: 0=NW, 1=NE, 2=SW, 3=SE.
                bool west = i == 0 || i == 2;
                bool north = i == 0 || i == 1;
                float x = west ? pad : Screen.width - width - pad;
                float y = north ? pad : Screen.height - height - pad;

                GUI.Box(new Rect(x, y, width, height), GUIContent.none);

                _nameStyle.normal.textColor = _camps != null ? _camps.ColorFor(i) : Color.white;
                GUI.Label(new Rect(x + 10, y + 6, width - 20, 20), player.Name, _nameStyle);

                GUI.Label(new Rect(x + width - 74, y + 6, 64, 20),
                    player.IsHuman ? "YOU" : (player.Profile != null ? player.Profile.Archetype : "CPU"),
                    _smallStyle);

                GUI.Label(new Rect(x + 10, y + 26, width - 20, 26), $"{camp.Vault:0} g banked", _vaultStyle);

                // Bag bar.
                var barBack = new Rect(x + 10, y + 54, width - 20, 12);
                GUI.Box(barBack, GUIContent.none);
                float fill = Mathf.Clamp01(player.BagFill);
                GUI.Box(new Rect(barBack.x, barBack.y, barBack.width * fill, barBack.height), GUIContent.none);
                GUI.Label(new Rect(barBack.x, barBack.y - 2, barBack.width, 16),
                    $"{player.Bag:0}/{player.BagCapacity:0}{(player.CanSteal ? "   THIEF" : "")}",
                    _smallStyle);
            }
        }

        private void DrawHints(MatchSimulation sim)
        {
            for (int i = 0; i < sim.Players.Count; i++)
            {
                PlayerState player = sim.Players[i];
                if (!player.IsHuman || player.Controller == null) continue;

                GUI.Label(new Rect(0, Screen.height - 46, Screen.width, 20),
                    $"{player.Name}: {player.Controller.Label}", _smallStyle);
                break;
            }

            GUI.Label(new Rect(0, Screen.height - 28, Screen.width, 20),
                "Tap punch = jab · Hold punch = charged smash · Bank gold at YOUR camp · P pause · R restart",
                _smallStyle);
        }

        private void DrawResults(MatchSimulation sim)
        {
            if (sim.Results == null) return;

            const float width = 520f;
            float height = 60f + sim.Results.Count * 26f;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x, panel.y + 8, panel.width, 30),
                $"{sim.Results[0].Name} WINS", _vaultStyle);

            for (int i = 0; i < sim.Results.Count; i++)
            {
                MatchResultRow row = sim.Results[i];
                var line = new Rect(panel.x + 20, panel.y + 46 + i * 26, panel.width - 40, 24);
                GUI.Label(line,
                    $"{row.Place}.  {row.Name,-12}  {row.Total,5:0}g   " +
                    $"mined {row.Stats.Mined,4:0}   robbed {row.Stats.Robbed,4:0}   " +
                    $"raids {row.Stats.VaultRaids}",
                    _smallStyle);
            }

            GUI.Label(new Rect(panel.x, panel.y + height - 24, panel.width, 20),
                "Press R for a rematch", _smallStyle);
        }

        private void EnsureStyles()
        {
            if (_clockStyle != null) return;

            _clockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _vaultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
            };
            _vaultStyle.normal.textColor = new Color(1f, 0.79f, 0.22f);
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            _smallStyle.normal.textColor = new Color(0.8f, 0.84f, 0.92f);
        }
    }
}
