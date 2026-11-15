using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Inventory;

namespace OutOfReach.Core
{
    /// <summary>
    /// R27 (Alex): a manifest over the launch mass limit may launch, and the ascent then fails: Core ends the
    /// mission at once as Lost with <see cref="OutOfReach.Simulation.SurvivalFailure.LaunchOverweight"/>. Only
    /// excess mass is let through; budget, power and the planned crew-day reserves stay hard launch limits, and the
    /// timed deadline and the sealed seat still launch valid manifests only. GAMEPLAY FICTION: no ascent physics.
    /// </summary>
    public static class OverweightLaunch
    {
        private const double Tolerance = 1e-9;

        public static bool IsOverweight(MissionConfiguration configuration)
        {
            return configuration != null && configuration.TotalMassKg > configuration.MassLimitKg + Tolerance;
        }

        /// <summary>True when excess mass is the configuration's only launch violation.</summary>
        public static bool IsOnlyOverweight(MissionConfiguration configuration)
        {
            if (!IsOverweight(configuration))
            {
                return false;
            }

            MissionParameters parameters = configuration.Parameters;
            ResourceSnapshot reserves = configuration.InitialResources;
            return configuration.TotalBudgetCredits <= configuration.BudgetLimitCredits + Tolerance
                && configuration.RequiredPowerWatts <= configuration.AvailablePowerWatts + Tolerance
                && reserves.OxygenKg + Tolerance >= parameters.MinimumOxygenKg
                && reserves.WaterLitres + Tolerance >= parameters.MinimumWaterLitres
                && reserves.FoodKg + Tolerance >= parameters.MinimumFoodKg;
        }

        /// <summary>The explained mission-loss reason for an overweight launch.</summary>
        public static string DescribeLoss(MissionConfiguration configuration)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Launch mass {0:0} kg exceeded the {1:0} kg limit. The vehicle failed to reach orbit and the crew was lost.",
                configuration.TotalMassKg, configuration.MassLimitKg);
        }
    }
}
