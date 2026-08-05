

namespace GoldHunter.Core.Input
{
    /// <summary>
    /// Edge-detected button with hold tracking. Punch uses the hold duration to
    /// decide jab vs charged smash, so the release must remember how long it was
    /// held rather than just that it went up.
    /// </summary>
    public sealed class ButtonState
    {
        /// <summary>Held right now.</summary>
        public bool IsDown { get; private set; }

        /// <summary>Went down this frame.</summary>
        public bool WasPressed { get; private set; }

        /// <summary>Came up this frame.</summary>
        public bool WasReleased { get; private set; }

        /// <summary>Seconds the button has been held.</summary>
        public float HeldTime { get; private set; }

        /// <summary>How long it had been held at the moment it was released.</summary>
        public float ReleaseHoldTime { get; private set; }

        public void Update(bool down, float dt)
        {
            WasPressed = down && !IsDown;
            WasReleased = !down && IsDown;
            if (WasReleased) ReleaseHoldTime = HeldTime;
            IsDown = down;
            HeldTime = down ? HeldTime + dt : 0f;
        }

        public void Reset()
        {
            IsDown = WasPressed = WasReleased = false;
            HeldTime = ReleaseHoldTime = 0f;
        }
    }
}
