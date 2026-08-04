using System.Collections.Generic;
using GoldHunter.Core.Events;
using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;
using UnityEngine;

namespace GoldHunter.Unity.Managers
{
    /// <summary>
    /// Owns the four vaults in the scene.
    ///
    /// Banking and raiding themselves are simulation rules (they involve more
    /// than one entity, so they live in the core); this component is the scene
    /// side of it — spawning the camp views, keeping their visuals in step with
    /// vault totals, and answering standings queries for the HUD.
    ///
    /// Camps are intentionally never given colliders: a camp has to stay
    /// walkable or its owner could never step in to deposit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BaseCampManager : MonoBehaviour
    {
        [Header("Presentation")]
        [Tooltip("Optional prefab for a camp. A simple marker is generated when empty.")]
        [SerializeField] private GameObject _campPrefab;

        [Tooltip("Colour per seat: NW, NE, SW, SE.")]
        [SerializeField]
        private Color[] _seatColors =
        {
            new Color(1f, 0.36f, 0.36f),
            new Color(0.31f, 0.65f, 1f),
            new Color(1f, 0.82f, 0.25f),
            new Color(0.36f, 0.9f, 0.55f),
        };

        [Header("Raid feedback")]
        [SerializeField] private float _alarmFlashSpeed = 6f;

        private BaseCampService _service;
        private StageManager _stage;
        private readonly List<Transform> _views = new List<Transform>();
        private readonly List<Renderer> _renderers = new List<Renderer>();

        public BaseCampService Service => _service;

        internal void Bind(BaseCampService service, StageManager stage)
        {
            _service = service;
            _stage = stage;
            BuildViews();
        }

        private void BuildViews()
        {
            foreach (Transform view in _views)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _views.Clear();
            _renderers.Clear();

            for (int i = 0; i < _service.Camps.Count; i++)
            {
                BaseCamp camp = _service.Camps[i];

                GameObject go = _campPrefab != null
                    ? Instantiate(_campPrefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

                go.name = $"BaseCamp_{i}";
                go.transform.SetParent(transform, false);
                go.transform.position = _stage.ToWorld(camp.Position);
                go.transform.localScale = new Vector3(camp.Radius * 2f, 0.05f, camp.Radius * 2f);

                // A camp must never block movement.
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = ColorFor(i) * 0.6f;
                    _renderers.Add(renderer);
                }
                else
                {
                    _renderers.Add(null);
                }
                _views.Add(go.transform);
            }
        }

        private void LateUpdate()
        {
            if (_service == null) return;

            for (int i = 0; i < _service.Camps.Count && i < _views.Count; i++)
            {
                BaseCamp camp = _service.Camps[i];
                Transform view = _views[i];
                if (view == null) continue;

                // Shake reads straight off simulation state, so every renderer agrees.
                Vector3 basePosition = _stage.ToWorld(camp.Position);
                if (camp.Shake > 0.001f)
                {
                    float t = Time.unscaledTime * 26f;
                    basePosition += new Vector3(Mathf.Sin(t * 3.3f), 0f, Mathf.Cos(t * 2.1f)) * (camp.Shake * 0.4f);
                }
                view.position = basePosition;

                Renderer renderer = _renderers[i];
                if (renderer == null) continue;

                Color color = ColorFor(i) * 0.6f;
                if (camp.Alarm > 0f)
                {
                    float pulse = (Mathf.Sin(Time.unscaledTime * _alarmFlashSpeed) * 0.5f + 0.5f) * camp.Alarm;
                    color = Color.Lerp(color, Color.red, pulse);
                }
                renderer.material.color = color;
            }
        }

        /* ---------------------------------------------------------- queries */

        public Color ColorFor(int seatIndex)
        {
            if (_seatColors == null || _seatColors.Length == 0) return Color.white;
            return _seatColors[Mathf.Clamp(seatIndex, 0, _seatColors.Length - 1)];
        }

        public float VaultOf(int seatIndex) => _service != null ? _service.CampOf(seatIndex).Vault : 0f;

        /// <summary>Whoever currently has the most banked. Bots gang up on them.</summary>
        public int LeaderIndex => _service != null ? _service.LeaderIndex() : 0;

        public float TotalBanked => _service != null ? _service.TotalBanked() : 0f;

        /// <summary>Seat indices ordered best-first, for the standings display.</summary>
        public List<int> Standings()
        {
            var order = new List<int>();
            if (_service == null) return order;

            for (int i = 0; i < _service.Camps.Count; i++) order.Add(i);
            order.Sort((a, b) => _service.CampOf(b).Vault.CompareTo(_service.CampOf(a).Vault));
            return order;
        }

        /// <summary>Reacts to a raid: kicks the view so the theft is unmissable.</summary>
        internal void OnVaultRaided(in VaultRaidedEvent evt)
        {
            int index = evt.Camp.OwnerIndex;
            if (index < 0 || index >= _views.Count) return;
            Transform view = _views[index];
            if (view != null) view.localScale *= 1.05f;
        }
    }
}
