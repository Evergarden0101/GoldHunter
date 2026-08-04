using GoldHunter.Core.Math;

namespace GoldHunter.Core.Input
{
    /// <summary>
    /// The only way anything drives a player.
    ///
    /// Humans and NPCs share this interface exactly: the AI writes into a
    /// <see cref="VirtualController"/> and the simulation cannot tell the
    /// difference. Never add a path that lets a brain move a player directly.
    /// </summary>
    public interface IController
    {
        /// <summary>Desired movement direction, magnitude clamped to 1.</summary>
        Vec2 Move { get; }

        /// <summary>Punch: tap for a jab, hold and release for a charged smash.</summary>
        ButtonState Attack { get; }

        /// <summary>Dash outside a shop; cycles the selection inside one.</summary>
        ButtonState Action { get; }

        /// <summary>Human-readable control hint, shown in the lobby and HUD.</summary>
        string Label { get; }

        /// <summary>Polls the underlying device (or the AI's intent) for this frame.</summary>
        void Poll(float dt);
    }
}
