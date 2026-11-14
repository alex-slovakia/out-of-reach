using System;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    public sealed class EmergencyModelTests
    {
        private static ShipPowerInputs Inputs(double power01, double lifeSupport01, bool batteryEmpty, double required = 1400d, double available = 1900d)
        {
            return new ShipPowerInputs(1, required, available, power01, lifeSupport01, batteryEmpty);
        }

        [Test]
        public void CabinCarbonDioxideRate_FollowsTheIdealGasLaw()
        {
            // n = P V / (R T) = 101,325 Pa x 20 m3 / (8.3145 x 295 K) = 826.2 mol of cabin air.
            Assert.That(CabinAtmosphere.GasMoles(20d, 101325d, 295d), Is.EqualTo(826.21d).Within(0.01d));
            // 1 kg CO2 = 22.72 mol, i.e. about 27,500 ppm of that air.
            Assert.That(CabinAtmosphere.PpmPerKilogramCarbonDioxide(), Is.EqualTo(27502d).Within(1d));
            // 1.01 kg per day (BVAD REV2 Table 3-23) is about 0.3215 ppm/s, roughly 1,157 ppm per hour.
            Assert.That(CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(1), Is.EqualTo(0.3215d).Within(0.0001d));
            Assert.That(CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(2), Is.EqualTo(2d * CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(1)).Within(1e-12));
        }

        [Test]
        public void ReferenceLevels_ConvertFromTheCitedPartialPressures()
        {
            Assert.That(AuroraMission.InitialCabinCarbonDioxidePpm, Is.EqualTo(2635d).Within(1d), "0.267 kPa");
            Assert.That(SurvivalRules.CarbonDioxideActionLevelPpm, Is.EqualTo(9968d).Within(1d), "1.01 kPa");
            Assert.That(SurvivalRules.CarbonDioxideLossLimitPpm, Is.EqualTo(26351d).Within(1d), "2.67 kPa");
            Assert.That(CabinAtmosphere.OxygenBufferSeconds(1) / 3600d, Is.EqualTo(19.4d).Within(0.1d), "21.2 to 18.6 kPa at 0.134 kPa per hour");
            Assert.That(CabinAtmosphere.OxygenBufferSeconds(0), Is.EqualTo(double.PositiveInfinity));
        }

        [Test]
        public void NominalConditions_ReproduceTheNominalRates_WithScrubbersHoldingTheBaseline()
        {
            EmergencyState state = EmergencyResourceModel.Evaluate(Inputs(1d, 1d, false));
            ResourceRates nominal = NominalResourceModel.Create(1, 1400d, 1900d);

            Assert.That(state.Rates.OxygenUseKgPerSecond, Is.EqualTo(nominal.OxygenUseKgPerSecond).Within(1e-15));
            Assert.That(state.Rates.WaterUseLitresPerSecond, Is.EqualTo(nominal.WaterUseLitresPerSecond).Within(1e-15));
            Assert.That(state.Rates.FoodUseKgPerSecond, Is.EqualTo(nominal.FoodUseKgPerSecond).Within(1e-15));
            Assert.That(state.Rates.NetLoadWatts, Is.EqualTo(-500d).Within(Fixtures.Tolerance), "surplus charges the battery");
            Assert.That(state.Rates.CarbonDioxideChangePpmPerSecond, Is.EqualTo(-EmergencyResourceModel.ScrubberDrawdownPpmPerSecond).Within(1e-12));
            Assert.That(state.Brownout, Is.False);
        }

        [Test]
        public void DamagedPower_ScalesGeneration_AndDamagedLifeSupport_StopsScrubbingShedsLoadAndLeaksOxygen()
        {
            EmergencyState state = EmergencyResourceModel.Evaluate(Inputs(0.2d, 0d, false));

            Assert.That(state.GenerationWatts, Is.EqualTo(380d).Within(Fixtures.Tolerance));
            Assert.That(state.LoadWatts, Is.EqualTo(1100d).Within(Fixtures.Tolerance));
            Assert.That(state.Rates.NetLoadWatts, Is.EqualTo(720d).Within(Fixtures.Tolerance));
            Assert.That(state.ScrubbingFraction01, Is.EqualTo(0d));
            Assert.That(state.Rates.CarbonDioxideChangePpmPerSecond, Is.EqualTo(CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(1)).Within(1e-12));
            double breathing = CrewMetabolicAssumptions.OxygenKgPerCrewDay / 86400d;
            double leak = EmergencyResourceModel.DamagedLifeSupportOxygenLeakKgPerDay / 86400d;
            Assert.That(state.Rates.OxygenUseKgPerSecond, Is.EqualTo(breathing + leak).Within(1e-15));
        }

        [Test]
        public void Brownout_StopsRepairedScrubbers_AndStoresNoEnergy()
        {
            EmergencyState state = EmergencyResourceModel.Evaluate(Inputs(0.2d, 1d, true));

            Assert.That(state.Brownout, Is.True, "1,400 W load, 380 W generation, empty battery");
            Assert.That(state.Rates.NetLoadWatts, Is.EqualTo(0d));
            Assert.That(state.ScrubbingFraction01, Is.EqualTo(0d));
            Assert.That(state.Rates.CarbonDioxideChangePpmPerSecond, Is.GreaterThan(0d));
        }

        [Test]
        public void EmptyBatteryWithSurplusGeneration_IsNotABrownout()
        {
            EmergencyState state = EmergencyResourceModel.Evaluate(Inputs(1d, 1d, true));

            Assert.That(state.Brownout, Is.False);
            Assert.That(state.Rates.NetLoadWatts, Is.LessThan(0d), "the battery recharges");
            Assert.That(state.Rates.CarbonDioxideChangePpmPerSecond, Is.LessThan(0d), "scrubbers run again");
        }

        [Test]
        public void InvalidModelInputs_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EmergencyResourceModel.Evaluate(Inputs(1.5d, 1d, false)));
            Assert.Throws<ArgumentOutOfRangeException>(() => EmergencyResourceModel.Evaluate(Inputs(1d, double.NaN, false)));
            Assert.Throws<ArgumentOutOfRangeException>(() => EmergencyResourceModel.Evaluate(Inputs(1d, 1d, false, -1d)));
            Assert.Throws<ArgumentOutOfRangeException>(() => EmergencyResourceModel.Evaluate(new ShipPowerInputs(-1, 1400d, 1900d, 1d, 1d, false)));
        }

        [Test]
        public void ScienceLoadout_ReachesBrownoutMuchSoonerThanRedundancy_AfterTheImpact()
        {
            using (var science = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            using (var redundancy = Fixtures.EmergencySession(AuroraMission.RedundancyPresetId))
            {
                // Science: 28 kWh against a 720 W deficit. Redundancy: 38 kWh against 420 W (no 300 W spectrometer).
                double scienceHours = science.Resources.TimeToNextDepletion() / 3600d;
                double redundancyHours = redundancy.Resources.TimeToNextDepletion() / 3600d;

                Assert.That(scienceHours, Is.EqualTo(28d / 0.72d).Within(1e-6), "about 38.9 h");
                Assert.That(redundancyHours, Is.EqualTo(38d / 0.42d).Within(1e-6), "about 90.5 h");
                Assert.That(science.Resources.Rates.NetLoadWatts, Is.GreaterThan(redundancy.Resources.Rates.NetLoadWatts));
            }
        }

        [Test]
        public void Repairs_RestoreTheRates_AndTheBatteryRecharges()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                session.AdvanceSimulation(3600d);
                double raised = session.Resources.Snapshot.CarbonDioxidePpm;
                Assert.That(raised, Is.GreaterThan(AuroraMission.InitialCabinCarbonDioxidePpm + 1000d), "an hour without scrubbers");

                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Assert.That(session.Resources.Rates.NetLoadWatts, Is.EqualTo(-500d).Within(Fixtures.Tolerance));

                session.AdvanceSimulation(6d * 3600d);

                Assert.That(session.Resources.Snapshot.CarbonDioxidePpm, Is.EqualTo(AuroraMission.InitialCabinCarbonDioxidePpm).Within(Fixtures.Tolerance), "drawn back down to the cabin baseline, not below");
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(28d).Within(Fixtures.Tolerance), "recharged to capacity");
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
            }
        }
    }
}
