using System;
using System.Globalization;

namespace OutOfReach.Simulation
{
    /// <summary>
    /// Constant rates applied by <see cref="ResourceLedger.AdvanceTime"/> until Core replaces them.
    /// "Use" rates deplete stored reserves and must be non-negative. NetLoadWatts is electrical load minus
    /// generation: positive drains the battery, negative charges it up to capacity. CarbonDioxide change is a
    /// cabin concentration rate (ppm/s), not an inventory flow.
    /// </summary>
    public readonly struct ResourceRates
    {
        public ResourceRates(
            double oxygenUseKgPerSecond,
            double waterUseLitresPerSecond,
            double foodUseKgPerSecond,
            double netLoadWatts,
            double carbonDioxideChangePpmPerSecond)
        {
            OxygenUseKgPerSecond = oxygenUseKgPerSecond;
            WaterUseLitresPerSecond = waterUseLitresPerSecond;
            FoodUseKgPerSecond = foodUseKgPerSecond;
            NetLoadWatts = netLoadWatts;
            CarbonDioxideChangePpmPerSecond = carbonDioxideChangePpmPerSecond;
        }

        public static ResourceRates Zero => new ResourceRates(0d, 0d, 0d, 0d, 0d);

        public double OxygenUseKgPerSecond { get; }

        public double WaterUseLitresPerSecond { get; }

        public double FoodUseKgPerSecond { get; }

        public double NetLoadWatts { get; }

        public double CarbonDioxideChangePpmPerSecond { get; }

        /// <summary>Null when valid; otherwise a short explanation of the first invalid field.</summary>
        public string FindProblem()
        {
            if (!IsFiniteNonNegative(OxygenUseKgPerSecond))
            {
                return Describe("Oxygen use", OxygenUseKgPerSecond, "kg/s");
            }

            if (!IsFiniteNonNegative(WaterUseLitresPerSecond))
            {
                return Describe("Water use", WaterUseLitresPerSecond, "L/s");
            }

            if (!IsFiniteNonNegative(FoodUseKgPerSecond))
            {
                return Describe("Food use", FoodUseKgPerSecond, "kg/s");
            }

            if (double.IsNaN(NetLoadWatts) || double.IsInfinity(NetLoadWatts))
            {
                return "Net electrical load must be finite.";
            }

            if (double.IsNaN(CarbonDioxideChangePpmPerSecond) || double.IsInfinity(CarbonDioxideChangePpmPerSecond))
            {
                return "Cabin CO2 change rate must be finite.";
            }

            return null;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static string Describe(string name, double value, string unit)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} must be finite and non-negative ({1} {2}).", name, value, unit);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "O2 {0:G6} kg/s, water {1:G6} L/s, food {2:G6} kg/s, net load {3:0.#} W, CO2 {4:G6} ppm/s",
                OxygenUseKgPerSecond,
                WaterUseLitresPerSecond,
                FoodUseKgPerSecond,
                NetLoadWatts,
                CarbonDioxideChangePpmPerSecond);
        }
    }
}
