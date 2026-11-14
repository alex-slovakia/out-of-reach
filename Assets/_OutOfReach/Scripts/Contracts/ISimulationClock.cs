namespace OutOfReach.Contracts
{
    /// <summary>
    /// Read-only view of Core's single simulation clock. TimeScale is simulated seconds per real
    /// second and retains its value while paused. Elapsed time delegates to authoritative mission state.
    /// Presentation reads these values; it must not advance a parallel clock.
    /// </summary>
    public interface ISimulationClock
    {
        bool IsPaused { get; }
        double TimeScale { get; }
        double ElapsedSimulationSeconds { get; }
    }
}
