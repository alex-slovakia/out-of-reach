using System;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// C06 gameplay assumptions, not a physiological model or propagated rescue orbit. Food/water portions use
    /// the existing BVAD-based crew-day quantities. A pre-grown seedling kit starts at 25 days, with 33 days as
    /// the nominal harvest age informed by NASA Veggie: https://science.nasa.gov/wp-content/uploads/2023/04/veggie_fact_sheet_508.pdf
    /// Ages are not a guaranteed crop schedule. 70 W is Veggie's cited approximate equipment load; this fictional
    /// module's recipe, water use, viability limits and tiny ration-equivalent yield are explicitly game tuning.
    /// No rescue arrival or Earth-to-Mars travel time is established by this spec.
    /// </summary>
    public sealed class SurvivalExtensionSpec
    {
        public SurvivalExtensionSpec(double portionHours = 6d, double hungerLimitHours = 48d, double thirstLimitHours = 24d,
            int buildSpareParts = 1, double buildEnergyKWh = .2d, double plantingWaterLitres = 1d,
            double seedlingAgeDays = 25d, double harvestAgeDays = 33d, double growPowerWatts = 70d,
            double growWaterLitresPerDay = .05d, double harvestFoodEquivalentKg = .05d, double timeScale = 300d)
        {
            PortionSeconds = Positive(portionHours, nameof(portionHours)) * 3600d;
            HungerLimitSeconds = Positive(hungerLimitHours, nameof(hungerLimitHours)) * 3600d;
            ThirstLimitSeconds = Positive(thirstLimitHours, nameof(thirstLimitHours)) * 3600d;
            if (!Finite(PortionSeconds) || !Finite(HungerLimitSeconds) || !Finite(ThirstLimitSeconds)
                || PortionSeconds >= HungerLimitSeconds || PortionSeconds >= ThirstLimitSeconds)
                throw new ArgumentException("One portion must fit strictly below each finite viability limit.");
            if (buildSpareParts < 1) throw new ArgumentOutOfRangeException(nameof(buildSpareParts));
            BuildSpareParts = buildSpareParts;
            BuildEnergyKWh = Positive(buildEnergyKWh, nameof(buildEnergyKWh));
            PlantingWaterLitres = Positive(plantingWaterLitres, nameof(plantingWaterLitres));
            SeedlingAgeDays = NonNegative(seedlingAgeDays, nameof(seedlingAgeDays));
            HarvestAgeDays = Positive(harvestAgeDays, nameof(harvestAgeDays));
            if (HarvestAgeDays <= SeedlingAgeDays || !Finite((HarvestAgeDays - SeedlingAgeDays) * CrewMetabolicAssumptions.SecondsPerDay))
                throw new ArgumentException("Harvest requires positive finite growing time after planting.");
            GrowPowerWatts = Positive(growPowerWatts, nameof(growPowerWatts));
            GrowWaterLitresPerDay = Positive(growWaterLitresPerDay, nameof(growWaterLitresPerDay));
            HarvestFoodEquivalentKg = Positive(harvestFoodEquivalentKg, nameof(harvestFoodEquivalentKg));
            if (!Finite(timeScale) || timeScale <= 0 || timeScale > SimulationClock.MaximumTimeScale)
                throw new ArgumentOutOfRangeException(nameof(timeScale));
            TimeScale = timeScale;
        }
        public double PortionSeconds { get; }
        public double HungerLimitSeconds { get; }
        public double ThirstLimitSeconds { get; }
        public int BuildSpareParts { get; }
        public double BuildEnergyKWh { get; }
        public double PlantingWaterLitres { get; }
        public double SeedlingAgeDays { get; }
        public double HarvestAgeDays { get; }
        public double GrowPowerWatts { get; }
        public double GrowWaterLitresPerDay { get; }
        /// <summary>Game ration-equivalent credit, not measured leafy biomass or a full dietary substitute.</summary>
        public double HarvestFoodEquivalentKg { get; }
        public double TimeScale { get; }
        public double GrowthSeconds => (HarvestAgeDays - SeedlingAgeDays) * CrewMetabolicAssumptions.SecondsPerDay;
        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static double NonNegative(double n, string name) => Finite(n) && n >= 0 ? n : throw new ArgumentOutOfRangeException(name);
        private static double Positive(double n, string name) => Finite(n) && n > 0 ? n : throw new ArgumentOutOfRangeException(name);
    }
}
