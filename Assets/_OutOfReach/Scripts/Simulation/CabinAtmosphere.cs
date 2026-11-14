namespace OutOfReach.Simulation
{
    /// <summary>
    /// DERIVED PHYSICS on labeled assumptions. The ideal gas law (n = P V / (R T)) converts exhaled CO2 mass into
    /// a cabin concentration change and gives the time the cabin's own air stays breathable after stored oxygen
    /// runs out. The cabin volume, pressure and temperature describe the fictional AURORA-7 (GAME ASSUMPTIONS).
    /// Partial-pressure reference levels are quoted from NASA BVAD REV2 (NASA/TP-2015-218570/REV2): Table 4-1 and
    /// section 4.1.1, which cites ISS flight rules. Concentrations are atmospheric state, never spendable inventory.
    /// </summary>
    public static class CabinAtmosphere
    {
        public const double GasConstantJoulesPerMoleKelvin = 8.314462618;

        public const double CarbonDioxideMolarMassKgPerMole = 0.04401;

        public const double OxygenMolarMassKgPerMole = 0.031999;

        /// <summary>GAME ASSUMPTION: free pressurized volume of the compact cabin, m³.</summary>
        public const double CabinFreeVolumeCubicMetres = 20d;

        /// <summary>GAME ASSUMPTION: sea-level-equivalent cabin pressure, kPa.</summary>
        public const double CabinPressureKPa = 101.325;

        /// <summary>GAME ASSUMPTION: cabin air temperature, K (about 22 °C).</summary>
        public const double CabinTemperatureKelvin = 295d;

        /// <summary>BVAD REV2 Table 4-1: lower crew-compartment p[CO2], kPa. Used as the cabin baseline.</summary>
        public const double BaselineCarbonDioxideKPa = 0.267;

        /// <summary>BVAD REV2 section 4.1.1 (ISS flight rules): action is required from this p[CO2], kPa.</summary>
        public const double ActionCarbonDioxideKPa = 1.01;

        /// <summary>BVAD REV2 section 4.1.1 (ISS flight rules): upper emergency p[CO2], kPa.</summary>
        public const double EmergencyCarbonDioxideKPa = 2.67;

        /// <summary>BVAD REV2 Table 4-1: nominal p[O2], kPa.</summary>
        public const double NominalOxygenKPa = 21.2;

        /// <summary>BVAD REV2 Table 4-1: p[O2] below which measurable impairment occurs before acclimatization, kPa.</summary>
        public const double ImpairmentOxygenKPa = 18.6;

        /// <summary>Moles of gas in a volume: n = P V / (R T), with pressure in Pa.</summary>
        public static double GasMoles(double volumeCubicMetres, double pressurePascals, double temperatureKelvin)
        {
            return pressurePascals * volumeCubicMetres / (GasConstantJoulesPerMoleKelvin * temperatureKelvin);
        }

        /// <summary>Concentration in ppm for a partial pressure in the AURORA-7 cabin.</summary>
        public static double PpmFromKilopascals(double partialPressureKPa)
        {
            return partialPressureKPa / CabinPressureKPa * 1000000d;
        }

        /// <summary>Cabin CO2 rise, in ppm, caused by adding one kilogram of CO2 to the cabin air (about 27,500).</summary>
        public static double PpmPerKilogramCarbonDioxide()
        {
            double carbonDioxideMoles = 1d / CarbonDioxideMolarMassKgPerMole;
            double airMoles = GasMoles(CabinFreeVolumeCubicMetres, CabinPressureKPa * 1000d, CabinTemperatureKelvin);
            return carbonDioxideMoles / airMoles * 1000000d;
        }

        /// <summary>Concentration rise per second with no CO2 removal (about 0.32 ppm/s for one crew member).</summary>
        public static double CrewCarbonDioxidePpmPerSecond(int crewCount)
        {
            double kilogramsPerSecond = crewCount * CrewMetabolicAssumptions.CarbonDioxideKgPerCrewDay / CrewMetabolicAssumptions.SecondsPerDay;
            return kilogramsPerSecond * PpmPerKilogramCarbonDioxide();
        }

        /// <summary>
        /// Seconds the cabin's own air stays above the impairment level once stored oxygen is exhausted: breathing
        /// lowers p[O2] from the nominal 21.2 kPa to 18.6 kPa (about 19.4 hours for one crew member in 20 m³).
        /// +Infinity without a crew.
        /// </summary>
        public static double OxygenBufferSeconds(int crewCount)
        {
            if (crewCount <= 0)
            {
                return double.PositiveInfinity;
            }

            double molesPerSecond = crewCount * CrewMetabolicAssumptions.OxygenKgPerCrewDay / OxygenMolarMassKgPerMole / CrewMetabolicAssumptions.SecondsPerDay;
            double pascalsPerSecond = molesPerSecond * GasConstantJoulesPerMoleKelvin * CabinTemperatureKelvin / CabinFreeVolumeCubicMetres;
            return (NominalOxygenKPa - ImpairmentOxygenKPa) * 1000d / pascalsPerSecond;
        }
    }
}
