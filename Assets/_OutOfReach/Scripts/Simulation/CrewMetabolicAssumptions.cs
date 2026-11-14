namespace OutOfReach.Simulation
{
    /// <summary>
    /// GAMEPLAY TUNING, not sourced physiology. Rounded per-crew-day consumption used for launch supply
    /// minimums and nominal drains. The values are a plausible order of magnitude for a crewed vehicle,
    /// but they are not medical thresholds and are not NASA data. Before the mission report cites a source
    /// for them (for example NASA's Life Support Baseline Values and Assumptions Document), the integrator
    /// must verify the exact figures and record them in NASA-DATA.md.
    /// </summary>
    public static class CrewMetabolicAssumptions
    {
        public const int CrewCount = 1;

        public const double SecondsPerDay = 86400d;

        /// <summary>Stored oxygen drawn per crew member per day, kg (reserve, not cabin percentage).</summary>
        public const double OxygenKgPerCrewDay = 0.84;

        /// <summary>Drinking and food-preparation water per crew member per day, litres; no recycling modeled yet.</summary>
        public const double WaterLitresPerCrewDay = 3.0;

        /// <summary>Packaged food per crew member per day, kg.</summary>
        public const double FoodKgPerCrewDay = 1.8;
    }
}
