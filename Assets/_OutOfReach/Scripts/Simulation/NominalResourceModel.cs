using System;

namespace OutOfReach.Simulation
{
    /// <summary>
    /// Pre-accident rates for a launched loadout. Crew use comes from <see cref="CrewMetabolicAssumptions"/>;
    /// net electrical load is load minus generation (a surplus charges the battery up to capacity). With
    /// working scrubbers, cabin CO2 is held steady (GAMEPLAY ASSUMPTION: removal balances crew output).
    /// The emergency model replaces these rates after the accident.
    /// </summary>
    public static class NominalResourceModel
    {
        public static ResourceRates Create(int crewCount, double requiredPowerWatts, double availablePowerWatts)
        {
            if (crewCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(crewCount), crewCount, "Crew count cannot be negative.");
            }

            if (double.IsNaN(requiredPowerWatts) || double.IsInfinity(requiredPowerWatts) || requiredPowerWatts < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredPowerWatts), requiredPowerWatts, "Load must be finite and non-negative.");
            }

            if (double.IsNaN(availablePowerWatts) || double.IsInfinity(availablePowerWatts) || availablePowerWatts < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(availablePowerWatts), availablePowerWatts, "Generation must be finite and non-negative.");
            }

            double perSecond = crewCount / CrewMetabolicAssumptions.SecondsPerDay;
            return new ResourceRates(
                CrewMetabolicAssumptions.OxygenKgPerCrewDay * perSecond,
                CrewMetabolicAssumptions.WaterLitresPerCrewDay * perSecond,
                CrewMetabolicAssumptions.FoodKgPerCrewDay * perSecond,
                requiredPowerWatts - availablePowerWatts,
                0d);
        }
    }
}
