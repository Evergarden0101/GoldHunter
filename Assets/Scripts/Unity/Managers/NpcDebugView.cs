using GoldHunter.Core.Ai;
using GoldHunter.Core.Math;
using UnityEngine;

namespace GoldHunter.Unity.Managers
{
    /// <summary>
    /// Draws what each bot is currently thinking: its A\* route, the waypoint it
    /// is heading for, and its current goal.
    ///
    /// Gizmos only, so it costs nothing in a build. Turn it on when a bot is
    /// doing something inexplicable — a route that stops short or a goal that
    /// never changes is usually the answer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcDebugView : MonoBehaviour
    {
        [SerializeField] private bool _drawPaths = true;
        [SerializeField] private bool _drawGoalLabels = true;

        [Tooltip("Highlights bots that have been unable to move for a moment.")]
        [SerializeField] private bool _highlightStuck = true;

        [SerializeField] private MatchManager _match;
        [SerializeField] private StageManager _stage;

        private void OnDrawGizmos()
        {
            if (!_drawPaths && !_drawGoalLabels && !_highlightStuck) return;

            if (_match == null) _match = GetComponent<MatchManager>();
            if (_stage == null) _stage = GetComponent<StageManager>();
            if (_match == null || _stage == null || !_stage.IsReady) return;

            var sim = _match.Simulation;
            if (sim == null) return;

            for (int i = 0; i < sim.Brains.Count; i++)
            {
                NpcBrain brain = sim.Brains[i];
                DrawBrain(brain);
            }
        }

        private void DrawBrain(NpcBrain brain)
        {
            var follower = brain.Follower;
            if (!follower.HasGoal) return;

            if (_drawPaths && follower.Path.Count > 0)
            {
                Gizmos.color = brain.PathFailed
                    ? new Color(1f, 0.3f, 0.3f, 0.9f)
                    : new Color(0.4f, 1f, 0.7f, 0.75f);

                for (int k = follower.WaypointIndex; k < follower.Path.Count - 1; k++)
                {
                    Gizmos.DrawLine(_stage.ToWorld(follower.Path[k], 0.4f),
                                    _stage.ToWorld(follower.Path[k + 1], 0.4f));
                }

                // The waypoint it is actively steering toward.
                Vec2 active = follower.Path[Mathf.Min(follower.WaypointIndex, follower.Path.Count - 1)];
                Gizmos.DrawWireSphere(_stage.ToWorld(active, 0.4f), 0.35f);
            }

            if (_highlightStuck && brain.StuckTimer > 0.2f)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
                Gizmos.DrawWireSphere(_stage.ToWorld(follower.Goal, 0.4f), 1.2f);
            }

#if UNITY_EDITOR
            if (_drawGoalLabels)
            {
                UnityEditor.Handles.Label(_stage.ToWorld(follower.Goal, 1.5f), brain.DebugLabel);
            }
#endif
        }
    }
}
