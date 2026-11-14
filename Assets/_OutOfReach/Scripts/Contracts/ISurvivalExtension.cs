using System;

namespace OutOfReach.Contracts
{
    /// <summary>Optional post-contact survival. Needs are unmet-demand state, never another reserve inventory.</summary>
    public interface ISurvivalExtension
    {
        bool Enabled { get; }
        bool IsActive { get; }
        double? ContactRestoredAtSimulationSeconds { get; }
        /// <summary>0 is satisfied; 1 reaches a fictional mission-viability limit, not a medical threshold.</summary>
        double Hunger01 { get; }
        double Thirst01 { get; }
        GrowModuleStage GrowStage { get; }
        double CropAgeSimulationDays { get; }
        double CropRemainingSimulationSeconds { get; }
        CommandResult TryConsumeRation();
        CommandResult TryDrinkWater();
        CommandResult TryBuildGrowModule();
        CommandResult TryPlant();
        CommandResult TryHarvest();
        event Action Changed;
    }
}
