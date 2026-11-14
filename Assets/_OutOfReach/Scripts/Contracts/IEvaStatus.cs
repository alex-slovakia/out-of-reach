using System;

namespace OutOfReach.Contracts
{
    /// <summary>Read-only view of Core's authoritative suit and airlock state. Durations are simulated seconds.</summary>
    public interface IEvaStatus
    {
        bool IsSuitOn { get; }
        bool IsSuitSealed { get; }
        double SuitOxygenKg { get; }
        double SuitOxygenCapacityKg { get; }
        double SuitOxygenSecondsRemaining { get; }
        bool IsOutside { get; }
        AirlockState Airlock { get; }
        double AirlockTransitionRemainingSeconds { get; }
        event Action Changed;
    }
}
