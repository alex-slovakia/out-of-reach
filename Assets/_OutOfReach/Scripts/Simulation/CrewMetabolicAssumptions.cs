namespace OutOfReach.Simulation
{
    /// <summary>
    /// Per-crew-member daily consumption. The figures are NASA reference values from the Life Support Baseline
    /// Values and Assumptions Document, NASA/TP-2015-218570/REV2 (February 2022,
    /// https://ntrs.nasa.gov/citations/20210024855), as checked by the team's science review (SCIENCE-01):
    /// <list type="bullet">
    /// <item>Oxygen 0.84 kg and carbon dioxide 1.01 kg per day: Table 3-23, reference crew member, 24-hour total.</item>
    /// <item>Water 2.5 kg per day: Table 4-20, transit vehicle, drinking 2.00 + food rehydration 0.50 (no hygiene water).</item>
    /// <item>Food 1.83 kg per day including packaging: Table 4-40.</item>
    /// </list>
    /// SIMPLIFICATIONS: one crew member and constant rates regardless of activity are game assumptions, and no
    /// water or oxygen is recycled. These are planning values, not medical thresholds.
    /// </summary>
    public static class CrewMetabolicAssumptions
    {
        /// <summary>GAME ASSUMPTION: AURORA-7 flies with a single crew member.</summary>
        public const int CrewCount = 1;

        public const double SecondsPerDay = 86400d;

        /// <summary>Stored oxygen drawn per crew member per day, kg (BVAD REV2 Table 3-23). A reserve, not cabin percentage.</summary>
        public const double OxygenKgPerCrewDay = 0.84;

        /// <summary>Drinking and food-rehydration water per crew member per day, litres (BVAD REV2 Table 4-20).</summary>
        public const double WaterLitresPerCrewDay = 2.5;

        /// <summary>Packaged food per crew member per day, kg (BVAD REV2 Table 4-40).</summary>
        public const double FoodKgPerCrewDay = 1.83;

        /// <summary>Exhaled carbon dioxide per crew member per day, kg (BVAD REV2 Table 3-23).</summary>
        public const double CarbonDioxideKgPerCrewDay = 1.01;
    }
}
