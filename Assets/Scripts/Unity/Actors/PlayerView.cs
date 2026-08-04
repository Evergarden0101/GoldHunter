using GoldHunter.Core.Simulation;
using GoldHunter.Unity.Managers;
using UnityEngine;

namespace GoldHunter.Unity.Actors
{
    /// <summary>
    /// The scene representation of one prospector: position, facing, the
    /// squash-and-stretch that sells a punch, and the charge glow.
    ///
    /// Purely presentational — it reads simulation state and never writes it.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        [SerializeField] private float _turnSmoothing = 18f;
        [SerializeField] private Transform _body;
        [SerializeField] private Renderer _renderer;

        private PlayerState _player;
        private StageManager _stage;
        private Color _baseColor;
        private Vector3 _baseScale;

        public PlayerState Player => _player;

        internal void Bind(PlayerState player, StageManager stage, Color color)
        {
            _player = player;
            _stage = stage;
            _baseColor = color;
            if (_body == null) _body = transform;
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null) _renderer.material.color = color;

            _baseScale = Vector3.one * (player.Radius * 2f);
            _body.localScale = _baseScale;
            transform.position = stage.ToWorld(player.Position, player.Radius);
        }

        private void LateUpdate()
        {
            if (_player == null) return;

            transform.position = _stage.ToWorld(_player.Position, _player.Radius);

            // Facing: simulation angle is on the XZ plane, measured like Atan2(y, x).
            Vector3 forward = new Vector3(Mathf.Cos(_player.Facing), 0f, Mathf.Sin(_player.Facing));
            if (forward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(forward, Vector3.up),
                    1f - Mathf.Exp(-_turnSmoothing * Time.unscaledDeltaTime));
            }

            // Squash and stretch, driven by the simulation's punch state machine.
            float squash = _player.Squash;
            float radius = _player.Radius * 2f;
            _body.localScale = new Vector3(radius / squash, radius * squash, radius / squash);

            if (_renderer == null) return;

            Color color = _baseColor;
            if (_player.HitFlash > 0f) color = Color.Lerp(color, Color.white, _player.HitFlash);
            else if (_player.IsCharging) color = Color.Lerp(color, Color.white, _player.ChargeRatio * 0.6f);
            else if (_player.Invulnerability > 0f && Mathf.FloorToInt(Time.unscaledTime * 24f) % 2 == 0)
            {
                color = Color.Lerp(color, Color.white, 0.3f);
            }
            _renderer.material.color = color;
        }

        private void OnDrawGizmosSelected()
        {
            if (_player == null || _stage == null) return;

            // Punch reach, so designers can see what the numbers mean.
            _player.GetPunchOrigin(out Core.Math.Vec2 origin, out float range);
            Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(_stage.ToWorld(origin, _player.Radius), range * 0.5f);
        }
    }
}
