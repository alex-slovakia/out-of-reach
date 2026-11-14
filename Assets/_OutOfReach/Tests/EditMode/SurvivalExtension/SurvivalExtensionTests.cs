using System;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    public sealed class SurvivalExtensionTests
    {
        private const double Day = CrewMetabolicAssumptions.SecondsPerDay;
        private const double Portion = 6d * 3600d;

        private static MissionSession Contact(SurvivalExtensionSpec spec = null, bool extraKit = true, bool receive = true)
        {
            var session = new MissionSession(survivalExtension: true, survivalSpec: spec);
            if (extraKit) Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 3).Succeeded, Is.True);
            Fixtures.Stabilize(session);
            Fixtures.CompleteEva(session);
            Fixtures.Transmit(session);
            if (receive) Assert.That(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds).Succeeded, Is.True);
            return session;
        }

        private static void Plant(MissionSession session)
        {
            Assert.That(session.Survival.TryBuildGrowModule().Succeeded, Is.True);
            Assert.That(session.Survival.TryPlant().Succeeded, Is.True);
        }

        private static void SpendDown(MissionSession session, ResourceId id, double leave)
        {
            double cost = session.Resources.GetQuantity(id) - leave;
            Assert.That(cost, Is.GreaterThanOrEqualTo(0d));
            Assert.That(session.Resources.TryConsume(new[] { new ResourceAmount(id, cost) }).Succeeded, Is.True);
        }

        [Test]
        public void DefaultStillEndsAtContactAndSerializedPhaseOrdinalsArePreserved()
        {
            Assert.That((int)GamePhase.Report, Is.EqualTo(7));
            Assert.That((int)GamePhase.Survival, Is.EqualTo(8));
            using (var session = Fixtures.RecoveredSession())
            {
                Assert.That(session.Survival.Enabled, Is.False);
                Assert.That(session.Survival.IsActive, Is.False);
                Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.False);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                Assert.That(session.Interactions.TryGet(InteractionIds.ConsumeRation, out _), Is.False);
            }
        }

        [Test]
        public void ContactIsAnOptInMidpointOnlyAfterTheActualLightTimeReply()
        {
            using (var session = Contact(receive: false))
            {
                Assert.That(session.State.TryTransition(GamePhase.Survival).Succeeded, Is.False);
                Assert.That(session.Survival.TryBuildGrowModule().Succeeded, Is.False);
                double replyAt = session.State.ElapsedSimulationSeconds + session.Communications.ReplyDelaySimulationSeconds;
                session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds - 1d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Transmission));
                Assert.That(session.Survival.ContactRestoredAtSimulationSeconds, Is.Null);
                session.AdvanceSimulation(1d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(session.Report, Is.Null);
                Assert.That(session.Survival.ContactRestoredAtSimulationSeconds, Is.EqualTo(replyAt).Within(1e-8));
                Assert.That(session.Survival.Hunger01, Is.Zero);
                Assert.That(session.Survival.Thirst01, Is.Zero);
                Assert.That(session.Clock.TimeScale, Is.EqualTo(300d));
                Assert.That(session.IsPlayerControlAllowed, Is.True);
                Assert.That(session.Interactions.TryGet(InteractionIds.ConsumeRation, out _), Is.True);
            }
        }

        [Test]
        public void JumpAcrossContactAccruesOnlyPostContactNeeds()
        {
            using (var session = Contact(receive: false))
            {
                double until = session.Communications.ReplyDelaySimulationSeconds;
                double before = session.State.ElapsedSimulationSeconds;
                session.AdvanceSimulation(until + Portion);
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(before + until + Portion).Within(1e-8));
                Assert.That(session.Survival.Hunger01, Is.EqualTo(.125d).Within(1e-9));
                Assert.That(session.Survival.Thirst01, Is.EqualTo(.25d).Within(1e-9));
                Assert.That(session.Resources.ElapsedSimulationSeconds, Is.EqualTo(session.State.ElapsedSimulationSeconds).Within(1e-8));
            }
        }

        [Test]
        public void RationsAndWaterSpendOneLedgerOnceWithoutAutomaticDoubleChargeOrSpam()
        {
            using (var session = Contact())
            {
                ResourceSnapshot before = session.Resources.Snapshot;
                Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.False);
                Assert.That(session.Survival.TryDrinkWater().Succeeded, Is.False);
                session.AdvanceSimulation(Portion);
                ResourceSnapshot hungry = session.Resources.Snapshot;
                Assert.That(hungry.FoodKg, Is.EqualTo(before.FoodKg));
                Assert.That(hungry.WaterLitres, Is.EqualTo(before.WaterLitres));
                Assert.That(hungry.OxygenKg, Is.EqualTo(before.OxygenKg - .84d / 4d).Within(1e-8));
                int changes = 0;
                session.Events.ResourceChanged += _ => changes++;
                Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.True);
                Assert.That(session.Survival.Hunger01, Is.Zero);
                Assert.That(session.Survival.Thirst01, Is.EqualTo(.25d));
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(before.FoodKg - 1.83d / 4d).Within(1e-9));
                Assert.That(session.Resources.Snapshot.WaterLitres, Is.EqualTo(before.WaterLitres));
                Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.False);
                Assert.That(session.Survival.TryDrinkWater().Succeeded, Is.True);
                Assert.That(session.Survival.TryDrinkWater().Succeeded, Is.False);
                Assert.That(session.Survival.Thirst01, Is.Zero);
                Assert.That(session.Resources.Snapshot.WaterLitres, Is.EqualTo(before.WaterLitres - 2.5d / 4d).Within(1e-9));
                Assert.That(changes, Is.EqualTo(2));
            }
        }

        [TestCase(ResourceId.FoodKg)]
        [TestCase(ResourceId.WaterLitres)]
        public void ConsumingTheLastPortionDoesNotTriggerOldEmptyReserveLoss(ResourceId id)
        {
            using (var session = Contact())
            {
                session.AdvanceSimulation(Portion);
                SpendDown(session, id, id == ResourceId.FoodKg ? session.Survival.RationFoodKg : session.Survival.DrinkWaterLitres);
                var result = id == ResourceId.FoodKg ? session.Survival.TryConsumeRation() : session.Survival.TryDrinkWater();
                Assert.That(result.Succeeded, Is.True);
                Assert.That(session.Resources.GetQuantity(id), Is.Zero);
                session.AdvanceSimulation(1d);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(session.Survival.IsActive, Is.True);
                session.AdvanceSimulation(Portion - 1d);
                ResourceSnapshot before = session.Resources.Snapshot;
                double need = id == ResourceId.FoodKg ? session.Survival.Hunger01 : session.Survival.Thirst01;
                result = id == ResourceId.FoodKg ? session.Survival.TryConsumeRation() : session.Survival.TryDrinkWater();
                Assert.That(result.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot);
                Assert.That(id == ResourceId.FoodKg ? session.Survival.Hunger01 : session.Survival.Thirst01, Is.EqualTo(need));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LongJumpsStopExactlyAtUnmetNeedFailureDespiteFullStores(bool hungerFirst)
        {
            var spec = hungerFirst ? new SurvivalExtensionSpec(thirstLimitHours: 96d) : new SurvivalExtensionSpec();
            using (var session = Contact(spec))
            {
                double start = session.State.ElapsedSimulationSeconds;
                ResourceSnapshot before = session.Resources.Snapshot;
                session.AdvanceSimulation(100d * Day);
                double limit = hungerFirst ? spec.HungerLimitSeconds : spec.ThirstLimitSeconds;
                Assert.That(session.State.ElapsedSimulationSeconds - start, Is.EqualTo(limit).Within(1e-8));
                Assert.That(session.Director.LossCause, Is.EqualTo(hungerFirst ? SurvivalFailure.HungerLimit : SurvivalFailure.ThirstLimit));
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(before.FoodKg));
                Assert.That(session.Resources.Snapshot.WaterLitres, Is.EqualTo(before.WaterLitres));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.State.OutcomeReason, Does.Contain("game viability limit"));
                Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.False);
            }
        }

        [Test]
        public void PauseFreezesCalendarNeedsAndCropAndDisposedSessionCannotAct()
        {
            using (var session = Contact())
            {
                Plant(session);
                session.AdvanceSimulation(Portion);
                session.Clock.Pause();
                ResourceSnapshot before = session.Resources.Snapshot;
                DateTime date = session.Calendar.NowUtc;
                double crop = session.Survival.CropAgeSimulationDays;
                Assert.That(session.Tick(900d).Succeeded, Is.True);
                Assert.That(session.AdvanceSimulation(Day).Succeeded, Is.False);
                Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.False);
                Assert.That(session.Survival.TryDrinkWater().Succeeded, Is.False);
                Assert.That(session.Calendar.NowUtc, Is.EqualTo(date));
                Assert.That(session.Survival.CropAgeSimulationDays, Is.EqualTo(crop));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot);
                session.Clock.Resume();
                Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.True);
                session.Dispose();
                Assert.That(session.Survival.TryDrinkWater().Succeeded, Is.False);
                Assert.That(session.Survival.TryBuildGrowModule().Succeeded, Is.False);
                Assert.That(session.Survival.TryPlant().Succeeded, Is.False);
                Assert.That(session.Survival.TryHarvest().Succeeded, Is.False);
                Assert.That(session.Survival.IsActive, Is.False);
            }
            using (var fresh = new MissionSession(survivalExtension: true))
            {
                Assert.That(fresh.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Unbuilt));
                Assert.That(fresh.Survival.Hunger01, Is.Zero);
                Assert.That(fresh.Survival.ContactRestoredAtSimulationSeconds, Is.Null);
            }
        }

        [Test]
        public void GrowBuildRequiresAnActualValidPreflightTradeoffAndChargesExactlyOnce()
        {
            using (var defaultLoadout = Contact(extraKit: false))
            {
                Assert.That(defaultLoadout.Resources.Snapshot.SparePartsCount, Is.Zero);
                Assert.That(defaultLoadout.Survival.TryBuildGrowModule().Failure, Is.EqualTo(CommandFailure.InsufficientResources));
            }
            using (var session = Contact())
            {
                Assert.That(session.Planner.LaunchedConfiguration.TotalMassKg, Is.EqualTo(571d));
                Assert.That(session.Planner.LaunchedConfiguration.TotalBudgetCredits, Is.EqualTo(765d));
                ResourceSnapshot before = session.Resources.Snapshot;
                int changes = 0;
                session.Events.ResourceChanged += _ => changes++;
                Assert.That(session.Survival.TryPlant().Succeeded, Is.False);
                Assert.That(session.Survival.TryBuildGrowModule().Succeeded, Is.True);
                Assert.That(session.Survival.TryBuildGrowModule().Succeeded, Is.False);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(before.SparePartsCount - 1));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(before.BatteryKWh - .2d).Within(1e-9));
                Assert.That(changes, Is.EqualTo(1));
                double originalLoad = session.Director.PowerState.LoadWatts;
                Assert.That(session.Survival.TryPlant().Succeeded, Is.True);
                Assert.That(session.Survival.TryPlant().Succeeded, Is.False);
                Assert.That(session.Survival.TryHarvest().Succeeded, Is.False);
                Assert.That(session.Resources.Snapshot.WaterLitres, Is.EqualTo(before.WaterLitres - 1d));
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(before.FoodKg));
                Assert.That(session.Director.PowerState.LoadWatts, Is.EqualTo(originalLoad + 70d));
                Assert.That(session.Resources.Rates.WaterUseLitresPerSecond, Is.EqualTo(.05d / Day));
                Assert.That(session.Resources.Rates.FoodUseKgPerSecond, Is.Zero);
                Assert.That(session.Survival.CropAgeSimulationDays, Is.EqualTo(25d));
                Assert.That(session.Survival.CropRemainingSimulationSeconds, Is.EqualTo(8d * Day));
                Assert.That(changes, Is.EqualTo(2));
            }
        }

        [Test]
        public void ActualEightDayCultivationConsumesResourcesAndCreditsOneModestHarvest()
        {
            using (var session = Contact())
            {
                Plant(session);
                ResourceSnapshot planted = session.Resources.Snapshot;
                double start = session.State.ElapsedSimulationSeconds;
                for (int portion = 0; portion < 32; portion++)
                {
                    Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Growing));
                    Assert.That(session.AdvanceSimulation(Portion).Succeeded, Is.True);
                    Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.True);
                    Assert.That(session.Survival.TryDrinkWater().Succeeded, Is.True);
                }
                Assert.That(session.State.ElapsedSimulationSeconds - start, Is.EqualTo(8d * Day).Within(1e-8));
                Assert.That(session.Calendar.DayNumber, Is.EqualTo(9));
                Assert.That(session.Survival.CropAgeSimulationDays, Is.EqualTo(33d));
                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.HarvestReady));
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(planted.FoodKg - 8d * 1.83d).Within(1e-8));
                Assert.That(session.Resources.Snapshot.WaterLitres, Is.EqualTo(planted.WaterLitres - 8d * 2.5d - 8d * .05d).Within(1e-8));
                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(planted.OxygenKg - 8d * .84d).Within(1e-8));
                Assert.That(session.Resources.Rates.WaterUseLitresPerSecond, Is.Zero);
                double before = session.Resources.Snapshot.FoodKg;
                int credits = 0;
                session.Events.ResourceChanged += _ =>
                {
                    credits++;
                    Assert.That(session.Survival.TryHarvest().Succeeded, Is.False, "Cannot duplicate harvest through an observer.");
                };
                Assert.That(session.Survival.TryHarvest().Succeeded, Is.True);
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(before + .05d).Within(1e-9));
                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Harvested));
                Assert.That(session.Survival.TryHarvest().Succeeded, Is.False);
                Assert.That(session.Survival.TryPlant().Succeeded, Is.False);
                Assert.That(credits, Is.EqualTo(1));
            }
        }

        [Test]
        public void IrrigationLossFailsCropAtExactBoundaryWithoutCreatingFoodOrKillingFedCrew()
        {
            using (var session = Contact())
            {
                Plant(session);
                SpendDown(session, ResourceId.WaterLitres, .025d);
                double food = session.Resources.Snapshot.FoodKg;
                double load = session.Director.PowerState.LoadWatts;
                session.AdvanceSimulation(.5d * Day + 1d);
                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Failed));
                Assert.That(session.Survival.CropAgeSimulationDays, Is.EqualTo(25.5d).Within(1e-8));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(food));
                Assert.That(session.Resources.Rates.WaterUseLitresPerSecond, Is.Zero);
                Assert.That(session.Director.PowerState.LoadWatts, Is.EqualTo(load - 70d));
                Assert.That(session.Survival.TryHarvest().Succeeded, Is.False);
            }
        }

        [Test]
        public void AddedGrowLoadUsesActualBatteryAndBrownoutFailsCrop()
        {
            using (var session = Contact(new SurvivalExtensionSpec(growPowerWatts: 2000d)))
            {
                Plant(session);
                double watts = session.Resources.Rates.NetLoadWatts;
                Assert.That(watts, Is.GreaterThan(0d));
                SpendDown(session, ResourceId.BatteryKWh, .1d);
                double until = .1d * 3600000d / watts;
                session.AdvanceSimulation(until + 1d);
                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Failed));
                Assert.That(session.Survival.CropAgeSimulationDays, Is.EqualTo(25d + until / Day).Within(1e-8));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.GreaterThan(0d), "After crop failure its load drops and solar charges the same battery.");
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
            }
        }

        [Test]
        public void LargeAndSmallStepsAgreeAtCropMaturityAndPowerWaterTransitions()
        {
            var spec = new SurvivalExtensionSpec(hungerLimitHours: 480d, thirstLimitHours: 480d);
            using (var large = Contact(spec))
            using (var small = Contact(spec))
            {
                Plant(large);
                Plant(small);
                large.AdvanceSimulation(9d * Day);
                for (int i = 0; i < 216; i++) small.AdvanceSimulation(3600d);
                Fixtures.AssertSnapshot(large.Resources.Snapshot, small.Resources.Snapshot, 1e-7);
                Assert.That(large.Survival.GrowStage, Is.EqualTo(small.Survival.GrowStage));
                Assert.That(large.Survival.Hunger01, Is.EqualTo(small.Survival.Hunger01).Within(1e-9));
                Assert.That(large.Survival.CropAgeSimulationDays, Is.EqualTo(33d));
                Assert.That(large.State.ElapsedSimulationSeconds, Is.EqualTo(small.State.ElapsedSimulationSeconds).Within(1e-8));
            }
        }

        [Test]
        public void AFailingChangedSubscriberCannotInterruptCommittedStateOrLaterSubscribers()
        {
            using (var session = Contact())
            {
                int delivered = 0;
                session.Survival.Changed += () => throw new InvalidOperationException("Test observer failure");
                session.Survival.Changed += () => delivered++;
                session.AdvanceSimulation(Portion);
                Assert.That(session.Survival.TryConsumeRation().Succeeded, Is.True);
                Assert.That(session.Survival.Hunger01, Is.Zero);
                Assert.That(delivered, Is.EqualTo(2));
                Assert.That(session.Events.SubscriberFailureCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void ReportProjectsFullPortionsWithoutResumingAutomaticDebits()
        {
            using (var session = Contact())
            {
                SpendDown(session, ResourceId.FoodKg, 1.83d);
                ResourceSnapshot before = session.Resources.Snapshot;
                MissionReport report = session.BuildReport();
                Assert.That(report.Holding.LimitedBy, Is.EqualTo("food"));
                Assert.That(report.Holding.Days, Is.EqualTo(1d).Within(1e-9));
                Assert.That(report.ToPlainText(), Does.Contain("Holding projection"));
                Assert.That(session.Resources.Rates.FoodUseKgPerSecond, Is.Zero);
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot);
                session.AdvanceSimulation(2d * Day);
                Assert.That(session.Report.ToPlainText(), Does.Contain("Unmet water demand"));
            }
        }

        [Test]
        public void InvalidTuningAndHarvestCreditsAreRejectedWithoutMutation()
        {
            Assert.Throws<ArgumentException>(() => new SurvivalExtensionSpec(harvestAgeDays: 25d));
            Assert.Throws<ArgumentException>(() => new SurvivalExtensionSpec(portionHours: 24d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurvivalExtensionSpec(growPowerWatts: double.NaN));
            using (var session = Contact())
            {
                ResourceSnapshot before = session.Resources.Snapshot;
                Assert.That(session.Resources.TryCreditHarvestFood(double.NaN).Succeeded, Is.False);
                Assert.That(session.Resources.TryCreditHarvestFood(-1d).Succeeded, Is.False);
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot);
            }
        }
    }
}
