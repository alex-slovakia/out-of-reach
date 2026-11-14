namespace OutOfReach.Contracts
{
    /// <summary>
    /// Read-only view of Core's optional planning deadline, measured in active, unpaused REAL seconds.
    /// Core owns expiry and the atomic manual-launch/expiry race. At expiry it launches a valid current
    /// draft or the disclosed immutable last-valid loadout. Timed initialization requires a valid loadout.
    /// Practice mode disables the deadline before planning starts. UI never decrements this value itself.
    /// </summary>
    public interface IPlanningDeadline
    {
        bool IsEnabled { get; }
        double RemainingRealSeconds { get; }
        IMissionConfiguration LastValidConfiguration { get; }
        bool UsedLastValidLoadoutAtDeadline { get; }
    }
}
