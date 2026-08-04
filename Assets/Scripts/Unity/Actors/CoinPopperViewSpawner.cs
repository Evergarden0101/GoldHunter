using System.Collections.Generic;
using GoldHunter.Core.Simulation;
using GoldHunter.Unity.Managers;
using UnityEngine;

namespace GoldHunter.Unity.Actors
{
    /// <summary>
    /// Spawns a <see cref="CoinPopperView"/> for every popper the simulation built.
    ///
    /// Placement and rates come from the simulation, which reads them from the
    /// Game Config asset — so moving a popper or changing its popping speed is
    /// a config edit, not a scene edit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoinPopperViewSpawner : MonoBehaviour
    {
        [Tooltip("Optional prefab. A capsule is generated when empty.")]
        [SerializeField] private GameObject _popperPrefab;

        [SerializeField] private Color _motherlodeColor = new Color(0.55f, 0.45f, 0.8f);
        [SerializeField] private Color _smallPopperColor = new Color(0.42f, 0.38f, 0.65f);

        private readonly List<CoinPopperView> _views = new List<CoinPopperView>();

        public IReadOnlyList<CoinPopperView> Views => _views;

        internal void Bind(MatchSimulation sim, StageManager stage)
        {
            foreach (CoinPopperView view in _views)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _views.Clear();

            for (int i = 0; i < sim.Poppers.Count; i++)
            {
                CoinPopper popper = sim.Poppers[i];

                GameObject go = _popperPrefab != null
                    ? Instantiate(_popperPrefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Capsule);

                go.name = $"CoinPopper_{popper.Label}";
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(popper.Radius * 1.6f, popper.Radius, popper.Radius * 1.6f);

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = popper.Kind == Core.Config.PopperKind.Motherlode
                        ? _motherlodeColor
                        : _smallPopperColor;
                }

                CoinPopperView view = go.GetComponent<CoinPopperView>();
                if (view == null) view = go.AddComponent<CoinPopperView>();
                view.Bind(popper, stage);
                _views.Add(view);
            }
        }
    }
}
