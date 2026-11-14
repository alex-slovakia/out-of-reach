using System;

namespace OutOfReach.Contracts
{
    /// <summary>Read-only view of Core's field modifications. Durations are simulated seconds.</summary>
    public interface IFieldModifications
    {
        bool SciencePayloadSalvaged { get; }
        int AbsorberCartridgesCarried { get; }
        bool AbsorberInstalled { get; }
        double AbsorberRemainingSimulationSeconds { get; }
        event Action Changed;
    }
}
