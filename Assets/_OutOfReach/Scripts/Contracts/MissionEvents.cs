namespace OutOfReach.Contracts
{
    public readonly struct PhaseChanged
    {
        public GamePhase Previous { get; }
        public GamePhase Current { get; }
        public PhaseChanged(GamePhase previous, GamePhase current) { Previous = previous; Current = current; }
    }

    public readonly struct ResourceChanged
    {
        public ResourceSnapshot Snapshot { get; }
        public ResourceChanged(ResourceSnapshot snapshot) { Snapshot = snapshot; }
    }

    public readonly struct SystemConditionChanged
    {
        public ShipSystemId SystemId { get; }
        public double Condition01 { get; }
        public SystemConditionChanged(ShipSystemId systemId, double condition01) { SystemId = systemId; Condition01 = condition01; }
    }

    public readonly struct CommunicationChanged
    {
        public CommunicationState State { get; }
        public double ReplyRemainingSimulationSeconds { get; }
        public CommunicationChanged(CommunicationState state, double replyRemainingSimulationSeconds)
        { State = state; ReplyRemainingSimulationSeconds = replyRemainingSimulationSeconds; }
    }

    /// <summary>Core issues a unique sequence ID. Presentation responds with the matching ID exactly once.</summary>
    public readonly struct CinematicRequested
    {
        public CinematicId Cinematic { get; }
        public int SequenceId { get; }
        public CinematicRequested(CinematicId cinematic, int sequenceId) { Cinematic = cinematic; SequenceId = sequenceId; }
    }

    /// <summary>Core ignores stale/duplicate completions and uses a fallback timeout if presentation is unavailable.</summary>
    public readonly struct CinematicFinished
    {
        public CinematicId Cinematic { get; }
        public int SequenceId { get; }
        public bool WasSkipped { get; }
        public CinematicFinished(CinematicId cinematic, int sequenceId, bool wasSkipped)
        { Cinematic = cinematic; SequenceId = sequenceId; WasSkipped = wasSkipped; }
    }

    public readonly struct AirlockStateChanged
    {
        public AirlockState State { get; }
        public AirlockStateChanged(AirlockState state) { State = state; }
    }

    public readonly struct MissionEnded
    {
        public MissionOutcome Outcome { get; }
        public string Reason { get; }
        public ResourceSnapshot FinalResources { get; }
        public MissionEnded(MissionOutcome outcome, string reason, ResourceSnapshot finalResources)
        { Outcome = outcome; Reason = reason; FinalResources = finalResources; }
    }
}
