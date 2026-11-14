using System;
using OutOfReach.Contracts;

namespace OutOfReach.Inventory
{
    /// <summary>
    /// Authoritative per-unit definition of one selectable equipment item. Every number is a game-design
    /// assumption for the fictional AURORA-7 vessel; budget credits are fictional planning credits, not
    /// aerospace cost estimates. Clients select IDs and counts only; they never supply these values.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(
            string id,
            string displayName,
            string summary,
            int maxQuantity,
            double unitMassKg,
            double unitPowerWatts,
            double unitBudgetCredits,
            double generationWatts = 0d,
            double oxygenKg = 0d,
            double waterLitres = 0d,
            double foodKg = 0d,
            double batteryKWh = 0d,
            int spareParts = 0,
            string scienceObjective = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An equipment ID is required.", nameof(id));
            }

            if (maxQuantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxQuantity), maxQuantity, "At least one unit must be selectable.");
            }

            if (spareParts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spareParts), spareParts, "Spare parts cannot be negative.");
            }

            RequireNonNegative(unitMassKg, nameof(unitMassKg));
            RequireNonNegative(unitPowerWatts, nameof(unitPowerWatts));
            RequireNonNegative(unitBudgetCredits, nameof(unitBudgetCredits));
            RequireNonNegative(generationWatts, nameof(generationWatts));
            RequireNonNegative(oxygenKg, nameof(oxygenKg));
            RequireNonNegative(waterLitres, nameof(waterLitres));
            RequireNonNegative(foodKg, nameof(foodKg));
            RequireNonNegative(batteryKWh, nameof(batteryKWh));

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Summary = summary ?? string.Empty;
            MaxQuantity = maxQuantity;
            UnitMassKg = unitMassKg;
            UnitPowerWatts = unitPowerWatts;
            UnitBudgetCredits = unitBudgetCredits;
            GenerationWatts = generationWatts;
            OxygenKg = oxygenKg;
            WaterLitres = waterLitres;
            FoodKg = foodKg;
            BatteryKWh = batteryKWh;
            SpareParts = spareParts;
            ScienceObjective = string.IsNullOrWhiteSpace(scienceObjective) ? null : scienceObjective;
        }

        /// <summary>Stable identifier used by planner commands and saved reports.</summary>
        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>Short English explanation of the trade-off, for planner and report text.</summary>
        public string Summary { get; }

        /// <summary>Hard selection limit (for example mounting points); larger requests are rejected.</summary>
        public int MaxQuantity { get; }

        public double UnitMassKg { get; }

        /// <summary>Continuous electrical load of one operating unit, in watts.</summary>
        public double UnitPowerWatts { get; }

        public double UnitBudgetCredits { get; }

        /// <summary>Electrical generation capacity added by one unit, in watts.</summary>
        public double GenerationWatts { get; }

        /// <summary>Stored oxygen reserve added by one unit, in kilograms (not cabin oxygen percentage).</summary>
        public double OxygenKg { get; }

        public double WaterLitres { get; }

        public double FoodKg { get; }

        /// <summary>Battery storage added by one unit, in kWh; batteries launch fully charged.</summary>
        public double BatteryKWh { get; }

        /// <summary>Whole spare parts added by one unit.</summary>
        public int SpareParts { get; }

        /// <summary>Science objective enabled by this item, or null for engineering equipment.</summary>
        public string ScienceObjective { get; }

        public bool IsScience => ScienceObjective != null;

        /// <summary>Contract view of one selectable unit: quantity 1 with authoritative unit values.</summary>
        public EquipmentSelection ToCatalogEntry()
        {
            return new EquipmentSelection(Id, 1, UnitMassKg, UnitPowerWatts, UnitBudgetCredits);
        }

        private static void RequireNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(name, value, "Equipment values must be finite and non-negative.");
            }
        }
    }
}
