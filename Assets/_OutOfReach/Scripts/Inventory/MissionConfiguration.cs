using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Inventory
{
    /// <summary>
    /// Immutable loadout evaluated against authoritative catalog values and mission limits. An instance is a
    /// value snapshot: the planner replaces its draft instead of mutating it, and launch freezes a fresh copy.
    /// Equipment is exposed through a read-only wrapper in catalog order, so callers cannot mutate it.
    /// </summary>
    public sealed class MissionConfiguration : IMissionConfiguration
    {
        private const double Tolerance = 1e-9;

        private readonly Dictionary<string, int> quantities;
        private readonly CommandResult validation;

        private MissionConfiguration(
            MissionParameters parameters,
            Dictionary<string, int> quantities,
            IReadOnlyList<EquipmentSelection> equipment,
            IReadOnlyList<string> scienceObjectives,
            double totalMassKg,
            double totalBudgetCredits,
            double requiredPowerWatts,
            double availablePowerWatts,
            ResourceSnapshot initialResources,
            CommandResult validation)
        {
            Parameters = parameters;
            this.quantities = quantities;
            Equipment = equipment;
            ScienceObjectives = scienceObjectives;
            TotalMassKg = totalMassKg;
            TotalBudgetCredits = totalBudgetCredits;
            RequiredPowerWatts = requiredPowerWatts;
            AvailablePowerWatts = availablePowerWatts;
            InitialResources = initialResources;
            this.validation = validation;
        }

        public MissionParameters Parameters { get; }

        public string MissionId => Parameters.MissionId;

        public IReadOnlyList<EquipmentSelection> Equipment { get; }

        /// <summary>Selected payload mass in kg (vessel dry mass excluded).</summary>
        public double TotalMassKg { get; }

        public double MassLimitKg => Parameters.MassLimitKg;

        /// <summary>Fictional planning credits.</summary>
        public double TotalBudgetCredits { get; }

        public double BudgetLimitCredits => Parameters.BudgetLimitCredits;

        /// <summary>Continuous electrical load in watts: vessel housekeeping plus selected equipment.</summary>
        public double RequiredPowerWatts { get; }

        /// <summary>Electrical generation capacity in watts: vessel arrays plus selected generation.</summary>
        public double AvailablePowerWatts { get; }

        /// <summary>Launch reserves: vessel baseline plus loadout; batteries launch fully charged.</summary>
        public ResourceSnapshot InitialResources { get; }

        public string AstronomicalReferenceId => Parameters.AstronomicalReferenceId;

        /// <summary>Science objectives enabled by the selected payload, in catalog order.</summary>
        public IReadOnlyList<string> ScienceObjectives { get; }

        public bool HasSciencePayload => ScienceObjectives.Count > 0;

        public CommandResult Validate()
        {
            return validation;
        }

        public int GetQuantity(string equipmentId)
        {
            return equipmentId != null && quantities.TryGetValue(equipmentId, out int quantity) ? quantity : 0;
        }

        /// <summary>Copy of the selected counts (only entries above zero).</summary>
        public Dictionary<string, int> CopyQuantities()
        {
            return new Dictionary<string, int>(quantities, StringComparer.Ordinal);
        }

        /// <summary>
        /// Evaluates a loadout. Duplicate IDs in the input are summed; zero counts are ignored. Unknown IDs,
        /// negative counts and counts above an item's limit make the configuration invalid (InvalidInput).
        /// Mass, budget, power and supply violations are PreconditionsNotMet; the draft stays inspectable.
        /// </summary>
        public static MissionConfiguration Create(
            EquipmentCatalog catalog,
            MissionParameters parameters,
            IEnumerable<KeyValuePair<string, int>> selectedQuantities)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var inputProblems = new List<string>();
            var counts = new Dictionary<string, long>(StringComparer.Ordinal);
            if (selectedQuantities != null)
            {
                foreach (KeyValuePair<string, int> pair in selectedQuantities)
                {
                    if (!catalog.TryGet(pair.Key, out _))
                    {
                        inputProblems.Add($"Unknown equipment '{pair.Key ?? "<null>"}'.");
                        continue;
                    }

                    if (pair.Value < 0)
                    {
                        inputProblems.Add($"'{pair.Key}' cannot have a negative quantity ({pair.Value}).");
                        continue;
                    }

                    counts.TryGetValue(pair.Key, out long existing);
                    counts[pair.Key] = existing + pair.Value;
                }
            }

            var kept = new Dictionary<string, int>(StringComparer.Ordinal);
            var equipment = new List<EquipmentSelection>();
            var science = new List<string>();
            double massKg = 0d;
            double budgetCredits = 0d;
            double requiredWatts = parameters.BaseLoadWatts;
            double availableWatts = parameters.BaseGenerationWatts;
            ResourceSnapshot baseline = parameters.BaseReserves;
            double oxygenKg = baseline.OxygenKg;
            double waterLitres = baseline.WaterLitres;
            double foodKg = baseline.FoodKg;
            double batteryKWh = baseline.BatteryKWh;
            long spareParts = baseline.SparePartsCount;

            // Iterate in catalog order so totals and the exposed list are deterministic.
            foreach (EquipmentDefinition item in catalog.Definitions)
            {
                if (!counts.TryGetValue(item.Id, out long requested) || requested == 0)
                {
                    continue;
                }

                if (requested > item.MaxQuantity)
                {
                    inputProblems.Add($"'{item.Id}' allows at most {item.MaxQuantity} unit(s); {requested} requested.");
                    continue;
                }

                int quantity = (int)requested;
                kept.Add(item.Id, quantity);
                equipment.Add(new EquipmentSelection(item.Id, quantity, item.UnitMassKg, item.UnitPowerWatts, item.UnitBudgetCredits));
                massKg += item.UnitMassKg * quantity;
                budgetCredits += item.UnitBudgetCredits * quantity;
                requiredWatts += item.UnitPowerWatts * quantity;
                availableWatts += item.GenerationWatts * quantity;
                oxygenKg += item.OxygenKg * quantity;
                waterLitres += item.WaterLitres * quantity;
                foodKg += item.FoodKg * quantity;
                batteryKWh += item.BatteryKWh * quantity;
                spareParts += (long)item.SpareParts * quantity;
                if (item.IsScience)
                {
                    science.Add(item.ScienceObjective);
                }
            }

            int clampedParts = spareParts > int.MaxValue ? int.MaxValue : (int)spareParts;
            var initial = new ResourceSnapshot(oxygenKg, waterLitres, foodKg, batteryKWh, clampedParts, baseline.CarbonDioxidePpm);

            CommandResult result;
            if (inputProblems.Count > 0)
            {
                result = new CommandResult(false, CommandFailure.InvalidInput, string.Join(" ", inputProblems));
            }
            else
            {
                List<string> violations = FindViolations(parameters, massKg, budgetCredits, requiredWatts, availableWatts, initial);
                result = violations.Count > 0
                    ? new CommandResult(false, CommandFailure.PreconditionsNotMet, string.Join(" ", violations))
                    : new CommandResult(true, CommandFailure.None, "Loadout is within the mass, budget, power and supply limits.");
            }

            return new MissionConfiguration(
                parameters,
                kept,
                Array.AsReadOnly(equipment.ToArray()),
                Array.AsReadOnly(science.ToArray()),
                massKg,
                budgetCredits,
                requiredWatts,
                availableWatts,
                initial,
                result);
        }

        private static List<string> FindViolations(
            MissionParameters parameters,
            double massKg,
            double budgetCredits,
            double requiredWatts,
            double availableWatts,
            ResourceSnapshot initial)
        {
            var violations = new List<string>();
            if (massKg > parameters.MassLimitKg + Tolerance)
            {
                violations.Add($"Mass {Format(massKg, "#,0.0")} kg exceeds the {Format(parameters.MassLimitKg, "#,0.0")} kg launch limit by {Format(massKg - parameters.MassLimitKg, "#,0.0")} kg.");
            }

            if (budgetCredits > parameters.BudgetLimitCredits + Tolerance)
            {
                violations.Add($"Budget {Format(budgetCredits, "#,0")} credits exceeds the {Format(parameters.BudgetLimitCredits, "#,0")}-credit limit by {Format(budgetCredits - parameters.BudgetLimitCredits, "#,0")} credits.");
            }

            if (requiredWatts > availableWatts + Tolerance)
            {
                violations.Add($"Electrical load {Format(requiredWatts, "#,0")} W exceeds available generation {Format(availableWatts, "#,0")} W by {Format(requiredWatts - availableWatts, "#,0")} W.");
            }

            string days = Format(parameters.PlannedCrewDays, "0.#");
            if (initial.OxygenKg + Tolerance < parameters.MinimumOxygenKg)
            {
                violations.Add($"Oxygen reserve {Format(initial.OxygenKg, "0.0")} kg is below the {Format(parameters.MinimumOxygenKg, "0.0")} kg required for {days} planned crew-days.");
            }

            if (initial.WaterLitres + Tolerance < parameters.MinimumWaterLitres)
            {
                violations.Add($"Water reserve {Format(initial.WaterLitres, "0.0")} L is below the {Format(parameters.MinimumWaterLitres, "0.0")} L required for {days} planned crew-days.");
            }

            if (initial.FoodKg + Tolerance < parameters.MinimumFoodKg)
            {
                violations.Add($"Food reserve {Format(initial.FoodKg, "0.0")} kg is below the {Format(parameters.MinimumFoodKg, "0.0")} kg required for {days} planned crew-days.");
            }

            return violations;
        }

        private static string Format(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
