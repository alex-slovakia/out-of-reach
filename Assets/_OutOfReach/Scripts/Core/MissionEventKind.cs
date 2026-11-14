namespace OutOfReach.Core
{
    /// <summary>Identifies one typed channel of <see cref="MissionEventBus"/> for diagnostics and tests.</summary>
    public enum MissionEventKind
    {
        PhaseChanged,
        ResourceChanged,
        SystemConditionChanged,
        CommunicationChanged,
        CinematicRequested,
        CinematicFinished,
        AirlockStateChanged,
        MissionEnded
    }
}
