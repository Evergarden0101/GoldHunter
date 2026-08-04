using System;
using UnityEngine;

namespace GoldHunter.Unity.Input
{
    /// <summary>
    /// One seat's key bindings, editable in the Inspector.
    ///
    /// Note the dash key on the IJKL scheme is U, not P: P is the pause hotkey
    /// and player two would otherwise freeze the match every time they dashed.
    /// </summary>
    [Serializable]
    public class KeyboardScheme
    {
        public string Name = "WASD";

        public KeyCode Up = KeyCode.W;
        public KeyCode Left = KeyCode.A;
        public KeyCode Down = KeyCode.S;
        public KeyCode Right = KeyCode.D;

        [Tooltip("Tap for a jab, hold to charge a smash. In a shop, hold to buy.")]
        public KeyCode Attack = KeyCode.Space;

        [Tooltip("Dash outside a shop; cycles the selection inside one.")]
        public KeyCode Action = KeyCode.LeftShift;

        public string Describe() => $"{Name} · {Attack} punch · {Action} dash";

        public static KeyboardScheme Wasd() => new KeyboardScheme
        {
            Name = "WASD",
            Up = KeyCode.W, Left = KeyCode.A, Down = KeyCode.S, Right = KeyCode.D,
            Attack = KeyCode.Space, Action = KeyCode.LeftShift,
        };

        public static KeyboardScheme Ijkl() => new KeyboardScheme
        {
            Name = "IJKL",
            Up = KeyCode.I, Left = KeyCode.J, Down = KeyCode.K, Right = KeyCode.L,
            Attack = KeyCode.O, Action = KeyCode.U,
        };

        public static KeyboardScheme Arrows() => new KeyboardScheme
        {
            Name = "Arrows",
            Up = KeyCode.UpArrow, Left = KeyCode.LeftArrow,
            Down = KeyCode.DownArrow, Right = KeyCode.RightArrow,
            Attack = KeyCode.Slash, Action = KeyCode.Period,
        };

        public static KeyboardScheme Numpad() => new KeyboardScheme
        {
            Name = "Numpad",
            Up = KeyCode.Keypad8, Left = KeyCode.Keypad4,
            Down = KeyCode.Keypad5, Right = KeyCode.Keypad6,
            Attack = KeyCode.Keypad0, Action = KeyCode.KeypadPeriod,
        };
    }
}
