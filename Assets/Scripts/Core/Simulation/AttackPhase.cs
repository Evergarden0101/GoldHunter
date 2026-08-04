namespace GoldHunter.Core.Simulation
{
    /// <summary>
    /// Punch state machine. Only <see cref="Active"/> frames can connect, which
    /// is what makes windup readable and charged punches committal.
    /// </summary>
    public enum AttackPhase
    {
        Idle = 0,
        Windup = 1,
        Active = 2,
        Recover = 3,
    }
}
