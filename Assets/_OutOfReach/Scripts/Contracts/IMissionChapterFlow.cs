using System;

namespace OutOfReach.Contracts
{
    /// <summary>Authored beats of the approved day 1-30 loop. The day is always the existing mission calendar's.</summary>
    public enum MissionChapter { None, Accident, Consequences, Contact, RescueFailed, FinalDecision, Rendezvous }

    /// <summary>Why the last routine segment stopped. Distinct from RestStopReason (one sleep).</summary>
    public enum RoutineStopReason
    {
        None, ReachedChapter, PlayerCancelled, Danger, ResourceShortage, CropReady, CropFailed,
        DispatchReceived, DecisionRequired, PrerequisiteMissing, MissionEnded
    }

    public enum ChapterDecision { None, ScienceOrSalvage, AcknowledgeRescueFailure, GrowOrBeacon, GrowOrConserve }

    public enum ChapterChoice { None, PreserveScience, AcknowledgeRescueFailure, StopGrowForBeacon, KeepGrowRunning, ConserveReserves }

    public enum RescuePlan { None, FirstRescuePlanned, FirstRescueFailed, InTransitCraftCommitted }

    /// <summary>A received message. Id null = none yet. Times are simulation seconds.</summary>
    public readonly struct MissionDispatch
    {
        public MissionDispatch(string id, string contentId, double transmittedAtSimulationSeconds, double receivedAtSimulationSeconds)
        {
            Id = id; ContentId = contentId;
            TransmittedAtSimulationSeconds = transmittedAtSimulationSeconds; ReceivedAtSimulationSeconds = receivedAtSimulationSeconds;
        }
        public string Id { get; }
        public string ContentId { get; }
        public double TransmittedAtSimulationSeconds { get; }
        public double ReceivedAtSimulationSeconds { get; }
    }

    /// <summary>
    /// Approved chapter loop (opt-in). Routine = bounded, interruptible advance of the ONE session clock/ledger in
    /// which Core itself issues the real ration/drink commands when due and ordinary rests (max 8.5 h). Rejected
    /// commands change nothing. Presentation reads; it never advances time or charges resources.
    /// </summary>
    public interface IMissionChapterFlow
    {
        bool Enabled { get; }
        MissionChapter Chapter { get; }
        /// <summary>Beat the next routine would reach and its start time; None/null when no routine target exists.</summary>
        MissionChapter NextChapter { get; }
        double? NextChapterAtSimulationSeconds { get; }
        bool IsRoutineRunning { get; }
        double? RoutineStartedAtSimulationSeconds { get; }
        /// <summary>Actual committed stop time of the last segment (never rounded up to the target).</summary>
        double? RoutineStoppedAtSimulationSeconds { get; }
        RoutineStopReason LastRoutineStop { get; }
        /// <summary>English explanation of the last stop (for the montage card), empty when none.</summary>
        string LastRoutineStopText { get; }
        /// <summary>Real commands Core issued in the current/last segment (montage tally).</summary>
        int RoutineRations { get; }
        int RoutineDrinks { get; }
        int RoutineRests { get; }
        ChapterDecision PendingDecision { get; }
        RescuePlan Rescue { get; }
        /// <summary>Stable unique Id identifies a dispatch; consumers deduplicate by Id, not Revision.</summary>
        MissionDispatch LatestDispatch { get; }
        /// <summary>Increments on every committed change, for one-shot presentation cues.</summary>
        int Revision { get; }
        /// <summary>
        /// Entry rejects pause, EVA/open airlock, unresolved prerequisites and terminal state.
        /// Pause suspends an accepted routine without advancing time. Every stop restores the prior time scale.
        /// Hazard/death takes precedence at simultaneous meal, crop, dispatch or recovery boundaries.
        /// </summary>
        CommandResult CheckContinueRoutine();
        CommandResult TryContinueRoutine();
        /// <summary>Stops at the actual committed time; never rounds up or advances to the target.</summary>
        CommandResult TryCancelRoutine();
        CommandResult CheckChoose(ChapterChoice choice);
        CommandResult TryChoose(ChapterChoice choice);
        event Action Changed;
    }
}
