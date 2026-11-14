using System;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    public sealed class SurvivalTests
    {
        private static double SecondsToCarbonDioxideLimit()
        {
            return (SurvivalRules.CarbonDioxideLossLimitPpm - AuroraMission.InitialCabinCarbonDioxidePpm) / CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(1);
        }

        [Test]
        public void UnrepairedLifeSupport_LosesTheMissionToCarbonDioxide_AtTheExactTime_InOneJump()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                double expected = SecondsToCarbonDioxideLimit();
                Assert.That(expected / 3600d, Is.EqualTo(20.5d).Within(0.1d), "about 20.5 hours without scrubbers");

                CommandResult jump = session.AdvanceSimulation(100d * 3600d);

                Assert.That(jump.Succeeded, Is.True);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.CarbonDioxideLimit));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(expected).Within(1e-3), "the jump stops at the fatal boundary");
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(expected).Within(1e-3), "no time passes after the loss");
                Assert.That(session.Resources.Snapshot.CarbonDioxidePpm, Is.EqualTo(SurvivalRules.CarbonDioxideLossLimitPpm).Within(1e-3));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.GreaterThan(0d), "the battery had not run flat yet");
                Assert.That(session.State.OutcomeReason, Does.Contain("Life support was not repaired"));
                Assert.That(recorder.Endings, Has.Count.EqualTo(1));
                Assert.That(recorder.Endings[0].Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(recorder.Endings[0].FinalResources.CarbonDioxidePpm, Is.EqualTo(SurvivalRules.CarbonDioxideLossLimitPpm).Within(1e-3));
            }
        }

        [Test]
        public void TheSameLoss_HappensAtTheSameTime_WhenPlayedFrameByFrame()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                int frames = 0;
                while (session.State.IsSimulationRunning && frames < 100000)
                {
                    session.AdvanceSimulation(37d);
                    frames++;
                }

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(SecondsToCarbonDioxideLimit()).Within(1e-3));
            }
        }

        [Test]
        public void RepairedLifeSupport_ButDamagedPower_LosesLater_BecauseTheBatteryRunsFlat()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                double flatAt = 27d / 1.02d * 3600d;
                double expected = flatAt + SecondsToCarbonDioxideLimit();

                session.AdvanceSimulation(200d * 3600d);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.CarbonDioxideLimit));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(expected).Within(1e-3));
                Assert.That(session.State.OutcomeReason, Does.Contain("battery ran flat because power distribution was not repaired"));
            }
        }

        [Test]
        public void BothRepairs_Survive_ThenTheFirstExhaustedSupplyDecidesTheLongRun()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                ResourceSnapshot start = session.Resources.Snapshot;

                session.AdvanceSimulation(30d * 86400d);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "the loadout covers the planned 30 crew-days");

                session.AdvanceSimulation(30d * 86400d);

                // Science focus: food 64 kg at 1.83 kg/day lasts 34.97 days, before water (40 d) and oxygen (40.5 d).
                double foodDays = start.FoodKg / CrewMetabolicAssumptions.FoodKgPerCrewDay;
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.FoodDepleted));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(foodDays * 86400d).Within(1e-3));
                Assert.That(session.State.OutcomeReason, Does.Contain("Food reserve exhausted"));
            }
        }

        [Test]
        public void EmptyOxygenTanks_AreNotAnInstantEnding_TheCabinAirBuysHours()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                double oxygen = session.Resources.Snapshot.OxygenKg;
                Assert.That(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.OxygenKg, oxygen) }).Succeeded, Is.True);
                double buffer = CabinAtmosphere.OxygenBufferSeconds(1);

                session.AdvanceSimulation(buffer - 60d);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "one minute before the cabin air reaches the impairment level");

                session.AdvanceSimulation(3600d);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.OxygenDepleted));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(buffer).Within(1e-3), "exactly when the buffer runs out");
                Assert.That(session.State.OutcomeReason, Does.Contain("Stored oxygen ran out"));
            }
        }

        [Test]
        public void SurvivalRules_HaveAFixedPriority_AndIgnoreBatteryAndParts()
        {
            double buffer = CabinAtmosphere.OxygenBufferSeconds(1);
            double limit = SurvivalRules.CarbonDioxideLossLimitPpm;

            Assert.That(SurvivalRules.Evaluate(new ResourceSnapshot(0d, 0d, 0d, 0d, 0, limit), 0d, buffer, 1), Is.EqualTo(SurvivalFailure.OxygenDepleted));
            Assert.That(SurvivalRules.Evaluate(new ResourceSnapshot(0d, 0d, 0d, 0d, 0, limit), 0d, buffer - 10d, 1), Is.EqualTo(SurvivalFailure.CarbonDioxideLimit), "buffer not used up yet");
            Assert.That(SurvivalRules.Evaluate(new ResourceSnapshot(1d, 0d, 0d, 0d, 0, limit), null, 0d, 1), Is.EqualTo(SurvivalFailure.CarbonDioxideLimit));
            Assert.That(SurvivalRules.Evaluate(new ResourceSnapshot(1d, 0d, 0d, 0d, 0, 3000d), null, 0d, 1), Is.EqualTo(SurvivalFailure.WaterDepleted));
            Assert.That(SurvivalRules.Evaluate(new ResourceSnapshot(1d, 1d, 0d, 0d, 0, 3000d), null, 0d, 1), Is.EqualTo(SurvivalFailure.FoodDepleted));
            Assert.That(SurvivalRules.Evaluate(new ResourceSnapshot(1d, 1d, 1d, 0d, 0, 3000d), null, 0d, 1), Is.EqualTo(SurvivalFailure.None), "a flat battery and no parts are not endings by themselves");
        }

        [Test]
        public void AfterTheLoss_NothingCanBeSpentRepairedOrAdvanced()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                session.AdvanceSimulation(100d * 3600d);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                ResourceSnapshot final = session.Resources.Snapshot;

                Assert.That(Fixtures.Repair(session, ShipSystemId.LifeSupport).Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.SparePartsCount, 1d) }).Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.AdvanceSimulation(60d).Failure, Is.EqualTo(CommandFailure.WrongPhase));
                session.Resources.AdvanceTime(3600d);

                Fixtures.AssertSnapshot(final, session.Resources.Snapshot, 0d);
                Assert.That(session.Director.TryEndMission(MissionOutcome.RecoveryPossible, "too late").Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
            }
        }
    }
}
