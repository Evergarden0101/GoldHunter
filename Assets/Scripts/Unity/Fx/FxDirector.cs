using System.Collections.Generic;
using GoldHunter.Core.Events;
using GoldHunter.Core.Simulation;
using GoldHunter.Unity.Managers;
using UnityEngine;

namespace GoldHunter.Unity.Fx
{
    /// <summary>
    /// Turns simulation events into things you can see and hear.
    ///
    /// This is the only class that implements <see cref="ISimulationListener"/>,
    /// which is what lets the core stay engine-free: the simulation reports that
    /// a punch landed and this decides what a punch looks like.
    ///
    /// Hit-stop is requested by the core and applied to the sim clock there;
    /// everything here runs on unscaled time so impacts snap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FxDirector : MonoBehaviour, ISimulationListener
    {
        [Header("Camera shake")]
        [SerializeField] private Transform _cameraRig;
        [SerializeField] private float _shakeDecay = 1.9f;
        [SerializeField] private float _shakeStrength = 1.35f;

        [Header("Impacts")]
        [SerializeField] private ParticleSystem _punchParticles;
        [SerializeField] private ParticleSystem _goldParticles;

        [Header("Audio (optional)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _punchClip;
        [SerializeField] private AudioClip _coinClip;
        [SerializeField] private AudioClip _buyClip;
        [SerializeField] private AudioClip _alarmClip;

        [Header("Announcements")]
        [SerializeField] private bool _drawAnnouncements = true;

        private MatchSimulation _sim;
        private StageManager _stage;
        private BaseCampManager _camps;

        private float _trauma;
        private Vector3 _cameraHome;
        private string _banner;
        private float _bannerLife;
        private readonly List<string> _ticker = new List<string>();
        private GUIStyle _bannerStyle;
        private GUIStyle _tickerStyle;

        internal void Bind(MatchSimulation sim, StageManager stage, BaseCampManager camps)
        {
            _sim = sim;
            _stage = stage;
            _camps = camps;
            if (_cameraRig == null && Camera.main != null) _cameraRig = Camera.main.transform;
            if (_cameraRig != null) _cameraHome = _cameraRig.localPosition;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            _trauma = Mathf.Max(0f, _trauma - dt * _shakeDecay);
            if (_cameraRig != null)
            {
                float t2 = _trauma * _trauma;
                float t = Time.unscaledTime * 47f;
                _cameraRig.localPosition = _cameraHome + new Vector3(
                    t2 * _shakeStrength * Mathf.Sin(t),
                    t2 * _shakeStrength * Mathf.Cos(t * 1.13f),
                    0f);
            }

            if (_bannerLife > 0f) _bannerLife -= dt;
        }

        private void Shake(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        private void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (_audioSource == null || clip == null) return;
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, volume);
        }

        private void Burst(ParticleSystem system, Core.Math.Vec2 position, int count)
        {
            if (system == null || _stage == null) return;
            system.transform.position = _stage.ToWorld(position, 0.5f);
            system.Emit(count);
        }

        /* --------------------------------------------- ISimulationListener */

        public void OnPunchThrown(PlayerState attacker, float power)
        {
            Play(_punchClip, 0.4f + power * 0.4f, 1.1f - power * 0.3f);
        }

        public void OnPunchWhiffed(PlayerState attacker) { }

        public void OnPunchLanded(in PunchLandedEvent evt)
        {
            // The core already asked for the freeze; this is the rest of the feel.
            Shake(evt.Shake);
            Burst(_punchParticles, evt.ImpactPoint, 10 + Mathf.RoundToInt(evt.Power * 18f));
            if (evt.GoldRipped > 0f)
            {
                Burst(_goldParticles, evt.Victim.Position, Mathf.Min(16, 3 + (int)(evt.GoldRipped / 5f)));
            }
            Play(_punchClip, 0.5f + evt.Power * 0.5f, 1f - evt.Power * 0.25f);

            if (evt.IsCharged && evt.Power > 0.75f)
            {
                PushTicker($"{evt.Attacker.Name} SMASHED {evt.Victim.Name} for {evt.GoldRipped:0}g");
            }
        }

        public void OnVaultRaided(in VaultRaidedEvent evt)
        {
            Shake(0.45f);
            Burst(_goldParticles, evt.Camp.Position, 18);
            Play(_alarmClip, 0.7f);
            _camps?.OnVaultRaided(evt);
            PushTicker($"{evt.Thief.Name} raided {evt.Owner.Name}'s vault for {evt.Amount:0}g");
        }

        public void OnPopperPunched(in PopperPunchedEvent evt)
        {
            Shake(0.18f + evt.Power * 0.2f);
            Burst(_goldParticles, evt.Popper.Position, 8);
            Play(_coinClip, 0.5f, 1.3f);
        }

        public void OnPopperGenerated(CoinPopper popper) { }

        public void OnMined(in MinedEvent evt) { }

        public void OnDeposited(in DepositEvent evt)
        {
            Play(_coinClip, 0.5f, 0.9f);
            Burst(_goldParticles, evt.Camp.Position, 4);
        }

        public void OnPickupCollected(in PickupCollectedEvent evt)
        {
            if (evt.Player.IsHuman) Play(_coinClip, 0.3f, 1.2f);
        }

        public void OnPurchase(in PurchaseEvent evt)
        {
            Play(_buyClip, 0.6f);
            Burst(_goldParticles, evt.Buyer.Position, 12);
            if (evt.Item.Id == Core.Config.ItemId.Steal)
            {
                PushTicker($"{evt.Buyer.Name} bought STEAL — vaults are no longer safe");
            }
        }

        public void OnPurchaseRejected(in PurchaseRejectedEvent evt) { }

        public void OnDash(PlayerState player) { }

        public void OnShopEntered(PlayerState player, Shop shop) { }

        public void OnShopExited(PlayerState player, Shop shop) { }

        public void OnAnnouncement(AnnouncementKind kind, string text)
        {
            _banner = text;
            _bannerLife = kind == AnnouncementKind.CountdownTick ? 0.9f : 1.8f;
            if (kind == AnnouncementKind.GoldRush) Shake(0.6f);
        }

        public void OnTicker(string text, int playerIndex) => PushTicker(text);

        public void OnPhaseChanged(MatchPhase phase) { }

        public void OnMatchEnded(IReadOnlyList<MatchResultRow> results)
        {
            Shake(0.5f);
            if (results != null && results.Count > 0) PushTicker($"{results[0].Name} wins with {results[0].Total:0}g");
        }

        private void PushTicker(string line)
        {
            _ticker.Add(line);
            if (_ticker.Count > 4) _ticker.RemoveAt(0);
        }

        /* --------------------------------------------------------------- HUD */

        private void OnGUI()
        {
            if (!_drawAnnouncements || _sim == null) return;
            EnsureStyles();

            if (_bannerLife > 0f && !string.IsNullOrEmpty(_banner))
            {
                var rect = new Rect(0, Screen.height * 0.22f, Screen.width, 70);
                GUI.Label(rect, _banner, _bannerStyle);
            }

            for (int i = 0; i < _ticker.Count; i++)
            {
                var rect = new Rect(0, Screen.height - 110 + i * 18, Screen.width, 18);
                GUI.Label(rect, _ticker[i], _tickerStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_bannerStyle != null) return;
            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _bannerStyle.normal.textColor = new Color(1f, 0.82f, 0.25f);

            _tickerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
            };
            _tickerStyle.normal.textColor = new Color(0.87f, 0.9f, 0.97f);
        }
    }
}
