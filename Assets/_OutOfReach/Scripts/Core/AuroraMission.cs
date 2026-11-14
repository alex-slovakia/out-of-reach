using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Inventory;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// The AURORA-7 mission: authoritative equipment catalog, launch limits and two viable starting loadouts.
    /// Every number is a GAMEPLAY ASSUMPTION for a fictional vessel (credits are fictional). The trade-off the
    /// player faces: the spectrometer returns science but needs a solar wing for power, and together they cost
    /// mass and budget that would otherwise buy spare parts, batteries and oxygen for the emergency.
    /// </summary>
    public static class AuroraMission
    {
        public const string MissionId = "AURORA-7";

        /// <summary>dataset_id of the committed NASA/JPL Horizons snapshot (docs/data/horizons/manifest.json).</summary>
        public const string AstronomicalReferenceId = "jpl-horizons-earth-mars-2026-v1";

        public const string ScienceFocusPresetId = "science-focus";
        public const string RedundancyPresetId = "redundancy";

        public const string OxygenTank = "oxygen-tank";
        public const string WaterTank = "water-tank";
        public const string FoodLocker = "food-locker";
        public const string BatteryModule = "battery-module";
        public const string SolarWing = "solar-wing";
        public const string SparePartsKit = "spare-parts-kit";
        public const string Spectrometer = "spectrometer";

        /// <summary>Payload allowance, kg (vessel dry mass excluded).</summary>
        public const double MassLimitKg = 600d;

        /// <summary>Fictional planning credits.</summary>
        public const double BudgetLimitCredits = 800d;

        /// <summary>Body-mounted array generation of the bare vessel, W.</summary>
        public const double BaseGenerationWatts = 1300d;

        /// <summary>Avionics, thermal control and life-support housekeeping load, W.</summary>
        public const double BaseLoadWatts = 1100d;

        /// <summary>Crew-days of consumables the launch review requires.</summary>
        public const double PlannedCrewDays = 30d;

        /// <summary>Initial cabin CO2 concentration, ppm. GAMEPLAY ASSUMPTION, not a health limit.</summary>
        public const double InitialCabinCarbonDioxidePpm = 1000d;

        public static MissionDefinition CreateDefinition()
        {
            var catalog = new EquipmentCatalog(new[]
            {
                new EquipmentDefinition(OxygenTank, "Oxygen tank",
                    "+12 kg stored oxygen for breathing and suit refills.",
                    maxQuantity: 6, unitMassKg: 30d, unitPowerWatts: 0d, unitBudgetCredits: 40d, oxygenKg: 12d),
                new EquipmentDefinition(WaterTank, "Water tank",
                    "+40 L water.",
                    maxQuantity: 6, unitMassKg: 48d, unitPowerWatts: 0d, unitBudgetCredits: 20d, waterLitres: 40d),
                new EquipmentDefinition(FoodLocker, "Food locker",
                    "+27 kg packaged food (15 crew-days).",
                    maxQuantity: 6, unitMassKg: 35d, unitPowerWatts: 0d, unitBudgetCredits: 25d, foodKg: 27d),
                new EquipmentDefinition(BatteryModule, "Battery module",
                    "+10 kWh storage to ride out a power loss.",
                    maxQuantity: 4, unitMassKg: 55d, unitPowerWatts: 0d, unitBudgetCredits: 60d, batteryKWh: 10d),
                new EquipmentDefinition(SolarWing, "Deployable solar wing",
                    "+600 W generation; recharges batteries and is needed to power the spectrometer.",
                    maxQuantity: 3, unitMassKg: 70d, unitPowerWatts: 0d, unitBudgetCredits: 120d, generationWatts: 600d),
                new EquipmentDefinition(SparePartsKit, "Spare parts kit",
                    "+3 spare parts for field repairs.",
                    maxQuantity: 5, unitMassKg: 25d, unitPowerWatts: 0d, unitBudgetCredits: 45d, spareParts: 3),
                new EquipmentDefinition(Spectrometer, "Atmospheric spectrometer",
                    "Science payload: 300 W continuous load and no survival value.",
                    maxQuantity: 1, unitMassKg: 90d, unitPowerWatts: 300d, unitBudgetCredits: 220d,
                    scienceObjective: "Measure Martian upper-atmosphere composition"),
            });

            int crew = CrewMetabolicAssumptions.CrewCount;
            double crewDays = PlannedCrewDays * crew;
            var parameters = new MissionParameters(
                MissionId,
                AstronomicalReferenceId,
                MassLimitKg,
                BudgetLimitCredits,
                BaseGenerationWatts,
                BaseLoadWatts,
                new ResourceSnapshot(
                    oxygenKg: 10d,
                    waterLitres: 20d,
                    foodKg: 10d,
                    batteryKWh: 8d,
                    sparePartsCount: 1,
                    carbonDioxidePpm: InitialCabinCarbonDioxidePpm),
                PlannedCrewDays,
                minimumOxygenKg: crewDays * CrewMetabolicAssumptions.OxygenKgPerCrewDay,
                minimumWaterLitres: crewDays * CrewMetabolicAssumptions.WaterLitresPerCrewDay,
                minimumFoodKg: crewDays * CrewMetabolicAssumptions.FoodKgPerCrewDay);

            var presets = new[]
            {
                new KeyValuePair<string, IReadOnlyDictionary<string, int>>(ScienceFocusPresetId, new Dictionary<string, int>
                {
                    { OxygenTank, 2 },
                    { WaterTank, 2 },
                    { FoodLocker, 2 },
                    { BatteryModule, 2 },
                    { SolarWing, 1 },
                    { SparePartsKit, 2 },
                    { Spectrometer, 1 },
                }),
                new KeyValuePair<string, IReadOnlyDictionary<string, int>>(RedundancyPresetId, new Dictionary<string, int>
                {
                    { OxygenTank, 3 },
                    { WaterTank, 2 },
                    { FoodLocker, 2 },
                    { BatteryModule, 3 },
                    { SolarWing, 1 },
                    { SparePartsKit, 4 },
                }),
            };

            return new MissionDefinition(catalog, parameters, crew, presets, ScienceFocusPresetId);
        }
    }
}
