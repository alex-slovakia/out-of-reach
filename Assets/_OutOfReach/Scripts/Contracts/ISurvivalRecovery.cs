using System;

namespace OutOfReach.Contracts
{
    public enum RestStopReason
    {
        None, Completed, Woken, FoodStoreEmpty, WaterStoreEmpty, OxygenStoreEmpty,
        PowerLost, CarbonDioxideDanger, CropReady, CropFailed, RecoveryMilestone, MissionEnded
    }

    public enum RecoveryStage { NotStarted, Inbound, BeaconRequested, Delayed, Arrived }

    /// <summary>
    /// Optional rest and fictional recovery on the existing authoritative mission clock.
    /// All times are simulation seconds, never wall time or a measured spacecraft ephemeris.
    /// Rejected commands leave state and reserves unchanged.
    /// </summary>
    public interface ISurvivalRecovery
    {
        bool IsResting { get; }
        double RestRemainingSimulationSeconds { get; }
        RestStopReason LastRestStop { get; }
        /// <summary>One-based day derived from the mission calendar, not a separate counter.</summary>
        int MissionDay { get; }
        RecoveryStage Stage { get; }
        double? ArrivalAtSimulationSeconds { get; }
        double ArrivalRemainingSimulationSeconds { get; }
        double? BeaconWindowEndsAtSimulationSeconds { get; }
        CommandResult CheckRest(double hours);
        CommandResult TryBeginRest(double hours);
        CommandResult TryWake();
        CommandResult CheckPowerBeacon();
        CommandResult TryPowerBeacon();
        event Action Changed;
    }
}
