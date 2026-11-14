using System;
using OutOfReach.Contracts;

namespace OutOfReach.Inventory
{
    /// <summary>
    /// Vessel baseline and launch constraints for one mission. Base reserves, generation and load belong to
    /// the vessel itself and are always present; the selected loadout adds to them. Payload mass excludes
    /// the vessel's own dry mass. All values are labeled game-design assumptions for a fictional vessel.
    /// </summary>
    public sealed class MissionParameters
    {
        public MissionParameters(
            string missionId,
            string astronomicalReferenceId,
            double massLimitKg,
            double budgetLimitCredits,
            double baseGenerationWatts,
            double baseLoadWatts,
            ResourceSnapshot baseReserves,
            double plannedCrewDays,
            double minimumOxygenKg,
            double minimumWaterLitres,
            double minimumFoodKg)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                throw new ArgumentException("A mission ID is required.", nameof(missionId));
            }

            if (string.IsNullOrWhiteSpace(astronomicalReferenceId))
            {
                throw new ArgumentException("An astronomical reference ID is required.", nameof(astronomicalReferenceId));
            }

            RequirePositive(massLimitKg, nameof(massLimitKg));
            RequirePositive(budgetLimitCredits, nameof(budgetLimitCredits));
            RequireNonNegative(baseGenerationWatts, nameof(baseGenerationWatts));
            RequireNonNegative(baseLoadWatts, nameof(baseLoadWatts));
            RequireNonNegative(plannedCrewDays, nameof(plannedCrewDays));
            RequireNonNegative(minimumOxygenKg, nameof(minimumOxygenKg));
            RequireNonNegative(minimumWaterLitres, nameof(minimumWaterLitres));
            RequireNonNegative(minimumFoodKg, nameof(minimumFoodKg));
            RequireNonNegative(baseReserves.OxygenKg, "baseReserves.OxygenKg");
            RequireNonNegative(baseReserves.WaterLitres, "baseReserves.WaterLitres");
            RequireNonNegative(baseReserves.FoodKg, "baseReserves.FoodKg");
            RequireNonNegative(baseReserves.BatteryKWh, "baseReserves.BatteryKWh");
            RequireNonNegative(baseReserves.CarbonDioxidePpm, "baseReserves.CarbonDioxidePpm");
            if (baseReserves.SparePartsCount < 0)
            {
                throw new ArgumentOutOfRangeException("baseReserves.SparePartsCount", baseReserves.SparePartsCount, "Spare parts cannot be negative.");
            }

            MissionId = missionId;
            AstronomicalReferenceId = astronomicalReferenceId;
            MassLimitKg = massLimitKg;
            BudgetLimitCredits = budgetLimitCredits;
            BaseGenerationWatts = baseGenerationWatts;
            BaseLoadWatts = baseLoadWatts;
            BaseReserves = baseReserves;
            PlannedCrewDays = plannedCrewDays;
            MinimumOxygenKg = minimumOxygenKg;
            MinimumWaterLitres = minimumWaterLitres;
            MinimumFoodKg = minimumFoodKg;
        }

        public string MissionId { get; }

        /// <summary>Dataset ID of the committed NASA/JPL reference used for communication timing.</summary>
        public string AstronomicalReferenceId { get; }

        public double MassLimitKg { get; }

        public double BudgetLimitCredits { get; }

        /// <summary>Generation capacity of the bare vessel, in watts.</summary>
        public double BaseGenerationWatts { get; }

        /// <summary>Continuous housekeeping load of the bare vessel (avionics, thermal, life support), in watts.</summary>
        public double BaseLoadWatts { get; }

        /// <summary>Reserves carried by the bare vessel; CarbonDioxidePpm is the initial cabin concentration.</summary>
        public ResourceSnapshot BaseReserves { get; }

        /// <summary>Crew-days of consumables the launch review requires.</summary>
        public double PlannedCrewDays { get; }

        public double MinimumOxygenKg { get; }

        public double MinimumWaterLitres { get; }

        public double MinimumFoodKg { get; }

        private static void RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(name, value, "Limits must be finite and positive.");
            }
        }

        private static void RequireNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(name, value, "Values must be finite and non-negative.");
            }
        }
    }
}
