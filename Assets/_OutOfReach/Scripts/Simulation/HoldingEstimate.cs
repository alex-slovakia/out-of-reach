using System;
using OutOfReach.Contracts;

namespace OutOfReach.Simulation
{
    /// <summary>
    /// ESTIMATE, not a prediction: how long the consumables last if nothing changes. It divides each reserve by its
    /// current use rate and takes the shortest. Used to describe the holding state after contact ("rescue has not
    /// arrived; the ship must hold for about N days") and what a lost ship still had.
    /// </summary>
    public readonly struct HoldingEstimate
    {
        public HoldingEstimate(double seconds, string limitedBy)
        {
            Seconds = seconds;
            LimitedBy = limitedBy ?? string.Empty;
        }

        /// <summary>Simulated seconds until the first consumable runs out; +Infinity if nothing is being used.</summary>
        public double Seconds { get; }

        public double Days => Seconds / CrewMetabolicAssumptions.SecondsPerDay;

        /// <summary>"oxygen", "water", "food" or "cabin CO2"; empty when unbounded.</summary>
        public string LimitedBy { get; }

        public bool IsBounded => !double.IsInfinity(Seconds);

        /// <summary>
        /// Shortest time to an empty oxygen, water or food reserve at the given rates, or to the cabin CO2 loss
        /// limit while CO2 is rising. The cabin oxygen buffer and the battery are not counted.
        /// </summary>
        public static HoldingEstimate Compute(ResourceSnapshot snapshot, ResourceRates rates)
        {
            double best = double.PositiveInfinity;
            string limitedBy = string.Empty;
            Consider(snapshot.OxygenKg, rates.OxygenUseKgPerSecond, "oxygen", ref best, ref limitedBy);
            Consider(snapshot.WaterLitres, rates.WaterUseLitresPerSecond, "water", ref best, ref limitedBy);
            Consider(snapshot.FoodKg, rates.FoodUseKgPerSecond, "food", ref best, ref limitedBy);
            if (rates.CarbonDioxideChangePpmPerSecond > 0d)
            {
                double headroom = Math.Max(0d, SurvivalRules.CarbonDioxideLossLimitPpm - snapshot.CarbonDioxidePpm);
                Consider(headroom, rates.CarbonDioxideChangePpmPerSecond, "cabin CO2", ref best, ref limitedBy);
            }

            return new HoldingEstimate(best, limitedBy);
        }

        private static void Consider(double quantity, double usePerSecond, string name, ref double best, ref string limitedBy)
        {
            if (usePerSecond <= 0d || double.IsNaN(usePerSecond) || double.IsNaN(quantity))
            {
                return;
            }

            double seconds = Math.Max(0d, quantity) / usePerSecond;
            if (seconds < best)
            {
                best = seconds;
                limitedBy = name;
            }
        }
    }
}
