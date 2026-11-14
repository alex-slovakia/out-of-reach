using System.Collections.Generic;

namespace OutOfReach.Contracts
{
    /// <summary>Frozen at launch. Implementations defensively copy selections; never expose mutable lists.</summary>
    public interface IMissionConfiguration
    {
        string MissionId { get; }
        IReadOnlyList<EquipmentSelection> Equipment { get; }
        double TotalMassKg { get; }
        double MassLimitKg { get; }
        double TotalBudgetCredits { get; }
        double BudgetLimitCredits { get; }
        double RequiredPowerWatts { get; }
        double AvailablePowerWatts { get; }
        ResourceSnapshot InitialResources { get; }
        string AstronomicalReferenceId { get; }
        CommandResult Validate();
    }
}
