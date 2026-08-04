using GoldHunter.Core.Math;

namespace GoldHunter.Core.Input
{
    /// <summary>
    /// A controller driven by code rather than hardware. The NPC brain sets
    /// <see cref="DesiredMove"/>, <see cref="WantAttack"/> and
    /// <see cref="WantAction"/>; polling turns them into the same edge-detected
    /// buttons a keyboard produces.
    /// </summary>
    public sealed class VirtualController : IController
    {
        public Vec2 DesiredMove;
        public bool WantAttack;
        public bool WantAction;

        public Vec2 Move { get; private set; }
        public ButtonState Attack { get; } = new ButtonState();
        public ButtonState Action { get; } = new ButtonState();
        public string Label { get; }

        public VirtualController(string label = "CPU")
        {
            Label = label;
        }

        public void Poll(float dt)
        {
            Vec2 m = DesiredMove;
            float mag = m.Magnitude;
            if (mag > 1f) m /= mag;
            Move = m;
            Attack.Update(WantAttack, dt);
            Action.Update(WantAction, dt);
        }

        public void Clear()
        {
            DesiredMove = Vec2.Zero;
            WantAttack = false;
            WantAction = false;
        }
    }
}
