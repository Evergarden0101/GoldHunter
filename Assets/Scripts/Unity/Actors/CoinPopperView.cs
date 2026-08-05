using GoldHunter.Core.Simulation;
using GoldHunter.Unity.Managers;
using UnityEngine;

namespace GoldHunter.Unity.Actors
{
    /// <summary>
    /// The scene representation of one coin popper.
    ///
    /// The machine shakes continuously while it is being drained, jolts each
    /// time it generates, and convulses when punched. Shake is read straight off
    /// simulation state rather than re-derived here, so every renderer and the
    /// tests agree on how hard it is rattling.
    ///
    /// To change how fast it pumps gold, edit Gold Per Minute on the Game Config
    /// asset — that is the popping speed, and nothing here hard-codes it.
    /// </summary>
    public sealed class CoinPopperView : MonoBehaviour
    {
        [Header("Shake")]
        [Tooltip("How far the machine rattles at full shake, in metres.")]
        [SerializeField] private float _shakeAmplitude = 0.42f;
        [SerializeField] private float _shakeFrequency = 22f;

        [Header("Fill")]
        [Tooltip("Scales the machine slightly as it fills with gold.")]
        [SerializeField] private bool _scaleWithFill = true;
        [SerializeField] private float _fillScaleRange = 0.15f;

        [Header("Readout")]
        [SerializeField] private bool _drawLabel = true;

        private CoinPopper _popper;
        private StageManager _stage;
        private Vector3 _homePosition;
        private Vector3 _homeScale;
        private float _phase;

        /// <summary>Current gold in the machine. Read-only mirror of the simulation.</summary>
        public float Gold => _popper != null ? _popper.Gold : 0f;

        /// <summary>The configured popping speed, for display and debugging.</summary>
        public float GoldPerMinute => _popper != null ? _popper.GoldPerMinute : 0f;

        internal void Bind(CoinPopper popper, StageManager stage)
        {
            _popper = popper;
            _stage = stage;
            _homePosition = stage.ToWorld(popper.Position, popper.Radius * 0.5f);
            transform.position = _homePosition;
            _homeScale = transform.localScale;
            _phase = Random.value * 10f;
        }

        private void LateUpdate()
        {
            if (_popper == null) return;

            // Presentation runs on unscaled time so a hit-stop freeze does not
            // also freeze the machine's rattle.
            _phase += Time.unscaledDeltaTime * (_shakeFrequency + _popper.Shake * 40f);

            float shake = _popper.Shake;
            transform.position = _homePosition + new Vector3(
                Mathf.Sin(_phase * 3.1f) * shake * _shakeAmplitude,
                0f,
                Mathf.Cos(_phase * 2.3f) * shake * _shakeAmplitude);

            if (_scaleWithFill)
            {
                float fill = _popper.FillRatio;
                transform.localScale = _homeScale * (1f + fill * _fillScaleRange + shake * 0.06f);
            }
        }

        private void OnGUI()
        {
            if (!_drawLabel || _popper == null || Camera.main == null) return;

            Vector3 screen = Camera.main.WorldToScreenPoint(_homePosition);
            if (screen.z <= 0f) return;

            var rect = new Rect(screen.x - 60f, Screen.height - screen.y + 20f, 120f, 34f);
            GUI.Label(rect, $"{_popper.Gold:0}g\n+{_popper.GoldPerMinute:0}/min", PopperLabelStyle);
        }

        private static GUIStyle _labelStyle;

        private static GUIStyle PopperLabelStyle
        {
            get
            {
                if (_labelStyle == null)
                {
                    _labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.UpperCenter,
                    };
                    _labelStyle.normal.textColor = new Color(1f, 0.79f, 0.22f);
                }
                return _labelStyle;
            }
        }
    }
}
