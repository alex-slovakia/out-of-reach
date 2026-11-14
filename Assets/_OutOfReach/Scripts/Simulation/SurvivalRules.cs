using OutOfReach.Contracts;

namespace OutOfReach.Simulation
{
    /// <summary>Why the crew can no longer continue. None while the mission can go on.</summary>
    public enum SurvivalFailure
    {
        None,
        OxygenDepleted,
        CarbonDioxideLimit,
        WaterDepleted,
        FoodDepleted,

        /// <summary>The sealed suit ran out of oxygen before the crew was back in a pressurized cabin.</summary>
        SuitOxygenDepleted,

        /// <summary>
        /// The ship is stable but the antenna can never be repaired (spare parts cannot be replenished), so no
        /// contact with Earth and no recovery plan is possible. Decided by Core, not by a reserve running out.
        /// </summary>
        ContactImpossible
    }

    /// <summary>
    /// GAMEPLAY ENDINGS anchored to cited operational levels; this is not a medical model and makes no lethality claim.
    /// <list type="bullet">
    /// <item>Carbon dioxide: the mission is declared lost when cabin CO2 reaches the upper ISS flight-rule emergency
    /// level quoted in NASA BVAD REV2 section 4.1.1 (2.67 kPa, about 26,350 ppm in this cabin). Action is required
    /// from 1.01 kPa (about 9,970 ppm), which the HUD can use as its caution level.</item>
    /// <item>Oxygen: an empty reserve is not an instant ending. The cabin's own air keeps p[O2] above the BVAD
    /// Table 4-1 impairment level (18.6 kPa) for a derived buffer time; the mission is lost when that runs out.</item>
    /// <item>Water or food exhausted: the mission cannot continue (SIMPLIFICATION, no physiology modeled).</item>
    /// <item>An empty battery is not an ending by itself: it causes a brownout, scrubbers stop and CO2 then decides.</item>
    /// </list>
    /// </summary>
    public static class SurvivalRules
    {
        /// <summary>Float slack so a step computed to land exactly on a limit counts as reaching it.</summary>
        public const double LimitTolerancePpm = 1e-6;

        /// <summary>Slack in seconds for the oxygen buffer boundary.</summary>
        public const double TimeToleranceSeconds = 1e-6;

        /// <summary>Cabin CO2 concentration at which the mission is declared lost, ppm (2.67 kPa in this cabin).</summary>
        public static readonly double CarbonDioxideLossLimitPpm = CabinAtmosphere.PpmFromKilopascals(CabinAtmosphere.EmergencyCarbonDioxideKPa);

        /// <summary>Cabin CO2 concentration from which action is required, ppm (1.01 kPa in this cabin). For HUD cautions.</summary>
        public static readonly double CarbonDioxideActionLevelPpm = CabinAtmosphere.PpmFromKilopascals(CabinAtmosphere.ActionCarbonDioxideKPa);

        /// <summary>
        /// First failure in a fixed order (oxygen, CO2, water, food), so equal-time ties are deterministic.
        /// </summary>
        /// <param name="snapshot">Current reserves and cabin CO2.</param>
        /// <param name="oxygenDepletedAtSeconds">Simulation time at which stored oxygen reached zero, or null.</param>
        /// <param name="nowSeconds">Current simulation time on the same clock.</param>
        /// <param name="crewCount">Crew size, for the cabin oxygen buffer.</param>
        public static SurvivalFailure Evaluate(ResourceSnapshot snapshot, double? oxygenDepletedAtSeconds, double nowSeconds, int crewCount)
        {
            if (snapshot.OxygenKg <= 0d && oxygenDepletedAtSeconds.HasValue
                && nowSeconds - oxygenDepletedAtSeconds.Value >= CabinAtmosphere.OxygenBufferSeconds(crewCount) - TimeToleranceSeconds)
            {
                return SurvivalFailure.OxygenDepleted;
            }

            if (snapshot.CarbonDioxidePpm >= CarbonDioxideLossLimitPpm - LimitTolerancePpm)
            {
                return SurvivalFailure.CarbonDioxideLimit;
            }

            if (snapshot.WaterLitres <= 0d)
            {
                return SurvivalFailure.WaterDepleted;
            }

            if (snapshot.FoodKg <= 0d)
            {
                return SurvivalFailure.FoodDepleted;
            }

            return SurvivalFailure.None;
        }
    }
}
