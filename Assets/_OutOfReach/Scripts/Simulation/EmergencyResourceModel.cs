using System;

namespace OutOfReach.Simulation
{
    /// <summary>Operational facts the ship model needs. Conditions come from Core's authoritative ship systems.</summary>
    public readonly struct ShipPowerInputs
    {
        public ShipPowerInputs(
            int crewCount,
            double requiredPowerWatts,
            double availablePowerWatts,
            double powerCondition01,
            double lifeSupportCondition01,
            bool batteryEmpty)
        {
            CrewCount = crewCount;
            RequiredPowerWatts = requiredPowerWatts;
            AvailablePowerWatts = availablePowerWatts;
            PowerCondition01 = powerCondition01;
            LifeSupportCondition01 = lifeSupportCondition01;
            BatteryEmpty = batteryEmpty;
        }

        public int CrewCount { get; }

        /// <summary>Configured continuous load (vessel housekeeping plus payload), W.</summary>
        public double RequiredPowerWatts { get; }

        /// <summary>Configured generation capacity (vessel arrays plus solar wings), W.</summary>
        public double AvailablePowerWatts { get; }

        /// <summary>0 = failed, 1 = fully operational. Scales the generation that reaches the bus.</summary>
        public double PowerCondition01 { get; }

        /// <summary>0 = failed, 1 = fully operational. Scales CO2 removal, its electrical load and the oxygen leak.</summary>
        public double LifeSupportCondition01 { get; }

        public bool BatteryEmpty { get; }
    }

    /// <summary>Result of one evaluation: the constant rates to apply and the facts that explain them.</summary>
    public readonly struct EmergencyState
    {
        public EmergencyState(ResourceRates rates, double generationWatts, double loadWatts, bool brownout, double scrubbingFraction01)
        {
            Rates = rates;
            GenerationWatts = generationWatts;
            LoadWatts = loadWatts;
            Brownout = brownout;
            ScrubbingFraction01 = scrubbingFraction01;
        }

        public ResourceRates Rates { get; }

        /// <summary>Generation that actually reaches the bus, W.</summary>
        public double GenerationWatts { get; }

        /// <summary>Load requested by running equipment, W.</summary>
        public double LoadWatts { get; }

        /// <summary>Battery empty while load exceeds generation: loads are shed and scrubbers stop.</summary>
        public bool Brownout { get; }

        /// <summary>Share of CO2 removal capacity currently running (0 during a brownout).</summary>
        public double ScrubbingFraction01 { get; }
    }

    /// <summary>
    /// One model for nominal flight and the emergency, so oxygen, battery energy, CO2 removal and repair state can
    /// never disagree. With both conditions at 1 it reproduces the nominal rates.
    /// <list type="bullet">
    /// <item>Generation reaching the bus = configured generation x power condition.</item>
    /// <item>Life support draws <see cref="LifeSupportLoadWatts"/> x its condition; the rest of the load is unaffected.</item>
    /// <item>An empty battery with load above generation is a brownout: nothing draws stored energy, scrubbers stop
    /// even if repaired, and the battery stays at zero until generation exceeds load again.</item>
    /// <item>Cabin CO2 changes by crew output (derived in <see cref="CabinAtmosphere"/>) minus removal capacity x the
    /// running share of the scrubbers. Fully running scrubbers draw CO2 down toward the cabin baseline.</item>
    /// <item>A damaged life-support pressure loop leaks stored oxygen in proportion to the damage.</item>
    /// <item>Breathing, drinking and eating continue regardless of ship damage.</item>
    /// </list>
    /// Pure and deterministic. Every constant here is GAMEPLAY TUNING for a fictional vessel, not NASA data.
    /// </summary>
    public static class EmergencyResourceModel
    {
        /// <summary>GAMEPLAY TUNING: life-support share of the vessel housekeeping load, W.</summary>
        public const double LifeSupportLoadWatts = 300d;

        /// <summary>GAMEPLAY TUNING: net CO2 drawdown with fully running scrubbers, ppm/s (stops at the cabin baseline).</summary>
        public const double ScrubberDrawdownPpmPerSecond = 0.5;

        /// <summary>GAMEPLAY TUNING: stored oxygen lost per day through a fully failed life-support pressure loop, kg.</summary>
        public const double DamagedLifeSupportOxygenLeakKgPerDay = 2d;

        public static EmergencyState Evaluate(ShipPowerInputs inputs)
        {
            if (inputs.CrewCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputs), inputs.CrewCount, "Crew count cannot be negative.");
            }

            if (!IsFiniteNonNegative(inputs.RequiredPowerWatts) || !IsFiniteNonNegative(inputs.AvailablePowerWatts))
            {
                throw new ArgumentOutOfRangeException(nameof(inputs), "Configured power must be finite and non-negative.");
            }

            if (!IsCondition(inputs.PowerCondition01) || !IsCondition(inputs.LifeSupportCondition01))
            {
                throw new ArgumentOutOfRangeException(nameof(inputs), "System conditions must be between 0 and 1.");
            }

            double generation = inputs.AvailablePowerWatts * inputs.PowerCondition01;
            double lifeSupportShare = Math.Min(LifeSupportLoadWatts, inputs.RequiredPowerWatts);
            double load = (inputs.RequiredPowerWatts - lifeSupportShare) + (lifeSupportShare * inputs.LifeSupportCondition01);
            double net = load - generation;
            bool brownout = inputs.BatteryEmpty && net > 0d;
            double scrubbing = brownout ? 0d : inputs.LifeSupportCondition01;

            double crewPerSecond = inputs.CrewCount / CrewMetabolicAssumptions.SecondsPerDay;
            double crewCarbonDioxide = CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(inputs.CrewCount);
            double removalCapacity = crewCarbonDioxide + ScrubberDrawdownPpmPerSecond;
            double oxygenLeak = DamagedLifeSupportOxygenLeakKgPerDay / CrewMetabolicAssumptions.SecondsPerDay * (1d - inputs.LifeSupportCondition01);

            var rates = new ResourceRates(
                (CrewMetabolicAssumptions.OxygenKgPerCrewDay * crewPerSecond) + oxygenLeak,
                CrewMetabolicAssumptions.WaterLitresPerCrewDay * crewPerSecond,
                CrewMetabolicAssumptions.FoodKgPerCrewDay * crewPerSecond,
                brownout ? 0d : net,
                crewCarbonDioxide - (removalCapacity * scrubbing));
            return new EmergencyState(rates, generation, load, brownout, scrubbing);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static bool IsCondition(double value)
        {
            return !double.IsNaN(value) && value >= 0d && value <= 1d;
        }
    }
}
