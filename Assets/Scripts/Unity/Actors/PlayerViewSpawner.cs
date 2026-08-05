using System.Collections.Generic;
using GoldHunter.Core.Simulation;
using GoldHunter.Unity.Managers;
using UnityEngine;

namespace GoldHunter.Unity.Actors
{
    /// <summary>Spawns a <see cref="PlayerView"/> per seat and colours it to match its camp.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerViewSpawner : MonoBehaviour
    {
        [Tooltip("Optional prefab. A sphere is generated when empty.")]
        [SerializeField] private GameObject _playerPrefab;

        private readonly List<PlayerView> _views = new List<PlayerView>();

        public IReadOnlyList<PlayerView> Views => _views;

        internal void Bind(MatchSimulation sim, StageManager stage, BaseCampManager camps)
        {
            foreach (PlayerView view in _views)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _views.Clear();

            for (int i = 0; i < sim.Players.Count; i++)
            {
                PlayerState player = sim.Players[i];

                GameObject go = _playerPrefab != null
                    ? Instantiate(_playerPrefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Sphere);

                go.name = $"Player_{player.Name}";
                go.transform.SetParent(transform, false);

                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);   // the simulation owns collision

                PlayerView view = go.GetComponent<PlayerView>();
                if (view == null) view = go.AddComponent<PlayerView>();

                Color color = camps != null ? camps.ColorFor(i) : Color.white;
                view.Bind(player, stage, color);
                _views.Add(view);
            }
        }
    }
}
