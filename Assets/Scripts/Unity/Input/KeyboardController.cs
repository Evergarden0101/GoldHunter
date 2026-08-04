using GoldHunter.Core.Input;
using GoldHunter.Core.Math;
using UnityEngine;

namespace GoldHunter.Unity.Input
{
    /// <summary>
    /// Adapts Unity's keyboard state to the core's <see cref="IController"/>.
    ///
    /// Reads are latched: a key pressed and released inside a single frame still
    /// registers, because polling alone would never see it down and the jab
    /// would vanish. That exact bug shipped in the original browser build.
    /// </summary>
    public sealed class KeyboardController : IController
    {
        private readonly KeyboardScheme _scheme;

        public Vec2 Move { get; private set; }
        public ButtonState Attack { get; } = new ButtonState();
        public ButtonState Action { get; } = new ButtonState();
        public string Label { get; }

        public KeyboardController(KeyboardScheme scheme)
        {
            _scheme = scheme;
            Label = scheme.Describe();
        }

        public void Poll(float dt)
        {
            float x = (Held(_scheme.Right) ? 1f : 0f) - (Held(_scheme.Left) ? 1f : 0f);
            float y = (Held(_scheme.Down) ? 1f : 0f) - (Held(_scheme.Up) ? 1f : 0f);

            var move = new Vec2(x, y);
            float magnitude = move.Magnitude;
            Move = magnitude > 1f ? move / magnitude : move;

            Attack.Update(Latched(_scheme.Attack), dt);
            Action.Update(Latched(_scheme.Action), dt);
        }

        private static bool Held(KeyCode key) => UnityEngine.Input.GetKey(key);

        /// <summary>Down this frame, or pressed at any point since the last frame.</summary>
        private static bool Latched(KeyCode key) =>
            UnityEngine.Input.GetKey(key) || UnityEngine.Input.GetKeyDown(key);
    }
}
