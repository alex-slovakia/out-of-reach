using System;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// GAME ASSUMPTIONS for AURORA-7's fictional suit and airlock. Where a figure follows a published reference it
    /// is named, but those references describe other designs (the ISS EMU and NASA's xEMU predictions in BVAD REV2
    /// section 4.6); they are not a universal minimum and nothing here is flight data. A solo EVA is itself a
    /// fictional emergency: the cited protocol requires at least two crew members.
    /// </summary>
    public sealed class EvaSpec
    {
        public EvaSpec(
            double suitPreparationSeconds = 8400d,
            double suitPurgeOxygenKg = 0.54d,
            double suitOxygenChargeKg = 0.30d,
            double suitOxygenUseKgPerHour = 0.10d,
            double airlockFreeVolumeCubicMetres = 4d,
            double airlockGasLossFraction = 0.10d,
            double airlockCycleEnergyKWh = 0.2d,
            double depressurizationSeconds = 600d,
            double repressurizationSeconds = 600d)
        {
            SuitPreparationSeconds = RequireNonNegative(suitPreparationSeconds, nameof(suitPreparationSeconds));
            SuitPurgeOxygenKg = RequireNonNegative(suitPurgeOxygenKg, nameof(suitPurgeOxygenKg));
            SuitOxygenChargeKg = RequirePositive(suitOxygenChargeKg, nameof(suitOxygenChargeKg));
            SuitOxygenUseKgPerHour = RequirePositive(suitOxygenUseKgPerHour, nameof(suitOxygenUseKgPerHour));
            AirlockFreeVolumeCubicMetres = RequireNonNegative(airlockFreeVolumeCubicMetres, nameof(airlockFreeVolumeCubicMetres));
            AirlockGasLossFraction = RequireNonNegative(airlockGasLossFraction, nameof(airlockGasLossFraction));
            if (AirlockGasLossFraction > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(airlockGasLossFraction), airlockGasLossFraction, "A fraction cannot exceed 1.");
            }

            AirlockCycleEnergyKWh = RequireNonNegative(airlockCycleEnergyKWh, nameof(airlockCycleEnergyKWh));
            DepressurizationSeconds = RequirePositive(depressurizationSeconds, nameof(depressurizationSeconds));
            RepressurizationSeconds = RequirePositive(repressurizationSeconds, nameof(repressurizationSeconds));
        }

        /// <summary>Suit-up and oxygen prebreathe, simulated seconds: 2 h 20 min (reference: ISS EMU exercise protocol, 80 + 60 min).</summary>
        public double SuitPreparationSeconds { get; }

        /// <summary>Stored oxygen spent purging the suit at suit-up, kg (reference: xEMU prediction of 0.54 kg per EVA).</summary>
        public double SuitPurgeOxygenKg { get; }

        /// <summary>Usable oxygen loaded into the suit tank at suit-up, kg.</summary>
        public double SuitOxygenChargeKg { get; }

        /// <summary>Oxygen breathed from the sealed suit, kg per hour (reference: xEMU prediction of 0.1 kg/h).</summary>
        public double SuitOxygenUseKgPerHour { get; }

        public double AirlockFreeVolumeCubicMetres { get; }

        /// <summary>Share of the airlock's air lost per cycle (reference: about 10 % in BVAD REV2 section 4.6.2).</summary>
        public double AirlockGasLossFraction { get; }

        /// <summary>Pump energy for one full airlock cycle, kWh.</summary>
        public double AirlockCycleEnergyKWh { get; }

        public double DepressurizationSeconds { get; }

        public double RepressurizationSeconds { get; }

        /// <summary>Stored oxygen drawn at suit-up: purge plus tank charge, kg.</summary>
        public double SuitPreparationOxygenKg => SuitPurgeOxygenKg + SuitOxygenChargeKg;

        public double SuitOxygenUseKgPerSecond => SuitOxygenUseKgPerHour / 3600d;

        /// <summary>Stored oxygen lost with the vented airlock air per cycle, kg (about 0.11 kg for 4 m³ at 10 %).</summary>
        public double AirlockCycleOxygenLossKg => CabinAtmosphere.OxygenKgInCabinAir(AirlockFreeVolumeCubicMetres) * AirlockGasLossFraction;

        private static double RequireNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(name, value, "EVA values must be finite and non-negative.");
            }

            return value;
        }

        private static double RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(name, value, "This EVA value must be finite and greater than zero.");
            }

            return value;
        }
    }
}
