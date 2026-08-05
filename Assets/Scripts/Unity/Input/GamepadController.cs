using GoldHunter.Core.Input;
using GoldHunter.Core.Math;
using UnityEngine;

namespace GoldHunter.Unity.Input
{
    /// <summary>
    /// Adapts a joystick to <see cref="IController"/> using the legacy Input
    /// Manager, so it works in a fresh project with no extra packages.
    ///
    /// Expects the default axes plus per-joystick buttons: button 0 punches,
    /// button 1 dashes.
    /// </summary>
    public sealed class GamepadController : IController
    {
        private const float DeadZone = 0.22f;

        private readonly int _joystickNumber;

        public Vec2 Move { get; private set; }
        public ButtonState Attack { get; } = new ButtonState();
        public ButtonState Action { get; } = new ButtonState();
        public string Label { get; }

        /// <param name="joystickNumber">1-based, matching Unity's joystick naming.</param>
        public GamepadController(int joystickNumber)
        {
            _joystickNumber = joystickNumber;
            Label = $"Gamepad {joystickNumber} · A punch · B dash";
        }

        public void Poll(float dt)
        {
            float x = ApplyDeadZone(SafeAxis("Horizontal"));
            float y = ApplyDeadZone(SafeAxis("Vertical"));

            // Unity's Vertical axis is +up; the simulation's +y points south.
            var move = new Vec2(x, -y);
            float magnitude = move.Magnitude;
            Move = magnitude > 1f ? move / magnitude : move;

            Attack.Update(Button(0), dt);
            Action.Update(Button(1), dt);
        }

        private bool Button(int index)
        {
            KeyCode code = KeyCode.Joystick1Button0 + (_joystickNumber - 1) * 20 + index;
            return UnityEngine.Input.GetKey(code);
        }

        private static float ApplyDeadZone(float v) => Mathf.Abs(v) < DeadZone ? 0f : v;

        private static float SafeAxis(string axis)
        {
            try
            {
                return UnityEngine.Input.GetAxisRaw(axis);
            }
            catch (System.ArgumentException)
            {
                // The axis is missing from the Input Manager; treat it as centred.
                return 0f;
            }
        }
    }
}
