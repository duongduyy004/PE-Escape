namespace KMA.Gameplay
{
    /// Read-only timing for penalty instructions, without a UI reference to the minigame assembly.
    public interface IFrogJumpTimingSource { float TimeLimitSeconds { get; } }
}
