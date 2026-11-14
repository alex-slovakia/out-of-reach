namespace OutOfReach.Simulation
{
    /// <summary>Unit conversions for stored electrical energy.</summary>
    public static class EnergyMath
    {
        /// <summary>1 kWh = 1000 W x 3600 s = 3,600,000 J (watt-seconds).</summary>
        public const double WattSecondsPerKilowattHour = 3600000d;

        /// <summary>energy_kWh = power_W x duration_s / 3,600,000.</summary>
        public static double KilowattHours(double watts, double seconds)
        {
            return watts * seconds / WattSecondsPerKilowattHour;
        }

        /// <summary>Battery energy change rate in kWh per second for a given power in watts.</summary>
        public static double KilowattHoursPerSecond(double watts)
        {
            return watts / WattSecondsPerKilowattHour;
        }
    }
}
