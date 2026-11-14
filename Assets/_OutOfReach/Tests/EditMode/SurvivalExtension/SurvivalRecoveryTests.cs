using System;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>
    /// C06-REST-RECOVERY R1: rest is an accelerated run of the one mission clock. While the crew rests, food and
    /// water leave the ledger at the BVAD crew-day rates, the needs neither accrue nor reset, and the rest stops at
    /// the first actual danger before any loss.
    /// </summary>
    public sealed class SurvivalRecoveryTests
    {
        private const double Hour = 3600d;

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        private static IInteractable Get(MissionSession session, string interactionId)
        {
            Assert.That(session.Interactions.TryGet(interactionId, out IInteractable interactable), Is.True, interactionId);
            return interactable;
        }

        /// <summary>The default loadout with the survival extension on, up to and (unless told otherwise) through contact.</summary>
        private static MissionSession Contact(bool receive = true, bool extraKit = false)
        {
            var session = new MissionSession(survivalExtension: true);
            if (extraKit)
            {
                Require(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 3));
            }

            Fixtures.Stabilize(session);
            Fixtures.CompleteEva(session);
            Fixtures.Transmit(session);
            if (receive)
            {
                Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
            }

            return session;
        }

        /// <summary>Begins the longest rest and advances (in half-hour chunks) until it stops for any reason.</summary>
        private static void RestUntilStop(MissionSession session)
        {
            SurvivalRecovery recovery = session.SurvivalRecovery;
            Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
            for (int guard = 0; recovery.IsResting; guard++)
            {
                Assert.That(guard, Is.LessThan(100), "a rest always ends");
                Require(session.AdvanceSimulation(Math.Min(1800d, recovery.RestRemainingSimulationSeconds)));
            }
        }

        private static string Value(MissionReport report, string sectionId, string label)
        {
            foreach (MissionReportSection section in report.Sections)
            {
                if (section.Id != sectionId)
                {
                    continue;
                }

                foreach (MissionReportLine line in section.Lines)
                {
                    if (line.Label == label)
                    {
                        return line.Value;
                    }
                }
            }

            return null;
        }

        private static int JournalCount(MissionSession session, string fragment)
        {
            int count = 0;
            foreach (MissionLogEntry entry in session.Director.Journal.Snapshot())
            {
                if (entry.Text.Contains(fragment))
                {
                    count++;
                }
            }

            return count;
        }

        private static void SpendDown(MissionSession session, ResourceId id, double leave)
        {
            double cost = session.Resources.GetQuantity(id) - leave;
            Assert.That(cost, Is.GreaterThanOrEqualTo(0d));
            Require(session.Resources.TryConsume(new[] { new ResourceAmount(id, cost) }));
        }

        private static double FoodPerSecond(MissionSession session)
        {
            return CrewMetabolicAssumptions.FoodKgPerCrewDay * session.Definition.CrewCount / CrewMetabolicAssumptions.SecondsPerDay;
        }

        private static double WaterPerSecond(MissionSession session)
        {
            return CrewMetabolicAssumptions.WaterLitresPerCrewDay * session.Definition.CrewCount / CrewMetabolicAssumptions.SecondsPerDay;
        }

        [Test]
        public void Rest_BelongsToAnActiveUnpausedSurvivalChapter_AndEveryRefusalChangesNothing()
        {
            using (MissionSession plain = Fixtures.RecoveredSession())
            {
                Assert.That(plain.SurvivalRecovery, Is.Null, "extension disabled: no rest service");
                Assert.That(plain.Interactions.TryGet(InteractionIds.SurvivalSleep, out _), Is.False);
            }

            using (MissionSession session = Contact(receive: false))
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                Assert.That(recovery.CheckRest(8d).Failure, Is.EqualTo(CommandFailure.WrongPhase), "not before contact");
                Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));

                ResourceSnapshot before = session.Resources.Snapshot;
                double scale = session.Clock.TimeScale;
                foreach (double hours in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 0d, 8.6d })
                {
                    Assert.That(recovery.TryBeginRest(hours).Failure, Is.EqualTo(CommandFailure.InvalidInput), hours.ToString());
                }

                Require(session.Clock.Pause());
                Assert.That(recovery.TryBeginRest(8d).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "paused");
                Require(session.Clock.Resume());
                Assert.That(recovery.TryWake().Succeeded, Is.False, "nobody to wake");

                Assert.That(recovery.IsResting, Is.False);
                Assert.That(recovery.RestCount, Is.EqualTo(0));
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.None));
                Assert.That(session.Clock.TimeScale, Is.EqualTo(scale));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);

                IInteractable sleep = Get(session, InteractionIds.SurvivalSleep);
                Assert.That(sleep.CanInteract, Is.True, sleep.Prompt);
                Require(sleep.TryInteract());
                Assert.That(recovery.IsResting, Is.True);
                Assert.That(recovery.RestRemainingSimulationSeconds, Is.EqualTo(SurvivalRecovery.MaximumRestHours * Hour).Within(1e-9));
                Require(sleep.TryInteract());
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Woken), "the same position wakes the crew");
            }
        }

        [Test]
        public void ARest_RunsTheSameClockFaster_DrawsBvadFoodAndWater_AndKeepsTheNeedsItFound()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                Require(session.AdvanceSimulation(7d * Hour));
                double hunger = session.Survival.Hunger01;
                double thirst = session.Survival.Thirst01;
                Assert.That(hunger, Is.GreaterThan(0d));
                ResourceSnapshot before = session.Resources.Snapshot;
                double start = session.State.ElapsedSimulationSeconds;
                int changes = 0;
                recovery.Changed += () => changes++;

                Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Assert.That(recovery.IsResting, Is.True);
                Assert.That(session.Clock.TimeScale, Is.EqualTo(SurvivalRecovery.RestTimeScale));
                Assert.That(recovery.RestRemainingSimulationSeconds, Is.EqualTo(8.5d * Hour).Within(1e-9));
                Assert.That(recovery.TryBeginRest(1d).Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "no second rest lease");
                Assert.That(session.Survival.TryConsumeRation().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "no manual portion while resting");
                Assert.That(session.Survival.TryDrinkWater().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(changes, Is.EqualTo(1));

                Require(session.Tick(8.5d));

                Assert.That(recovery.IsResting, Is.False);
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                Assert.That(session.Clock.TimeScale, Is.EqualTo(session.Survival.Spec.TimeScale), "waking restores the Survival time scale");
                Assert.That(session.State.ElapsedSimulationSeconds - start, Is.EqualTo(8.5d * Hour).Within(1e-3), "8.5 real seconds at 3,600");
                Assert.That(session.Survival.Hunger01, Is.EqualTo(hunger).Within(1e-12), "the needs neither accrue nor reset");
                Assert.That(session.Survival.Thirst01, Is.EqualTo(thirst).Within(1e-12));

                ResourceSnapshot after = session.Resources.Snapshot;
                Assert.That(before.FoodKg - after.FoodKg, Is.EqualTo(FoodPerSecond(session) * 8.5d * Hour).Within(1e-9), "BVAD food draw");
                Assert.That(before.WaterLitres - after.WaterLitres, Is.EqualTo(WaterPerSecond(session) * 8.5d * Hour).Within(1e-9), "BVAD water draw");
                Assert.That(after.OxygenKg, Is.LessThan(before.OxygenKg), "oxygen keeps running");
                Assert.That(recovery.RestedSimulationSeconds, Is.EqualTo(8.5d * Hour).Within(1e-3));
                Assert.That(recovery.RestCount, Is.EqualTo(1));
                Assert.That(recovery.MissionDay, Is.EqualTo(session.Calendar.DayNumber));
                Assert.That(changes, Is.EqualTo(2));

                Require(session.AdvanceSimulation(Hour));
                Assert.That(session.Survival.Hunger01, Is.GreaterThan(hunger), "awake again, the needs accrue");
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(after.FoodKg), "awake, food is spent only by commands");
            }
        }

        [TestCase(ResourceId.FoodKg, RestStopReason.FoodStoreEmpty)]
        [TestCase(ResourceId.WaterLitres, RestStopReason.WaterStoreEmpty)]
        public void ARest_StopsExactlyWhenAStoreRunsOut_AndTheChapterGoesOn(ResourceId store, RestStopReason expected)
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                double perSecond = store == ResourceId.FoodKg ? FoodPerSecond(session) : WaterPerSecond(session);
                SpendDown(session, store, perSecond * 3d * Hour);

                Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(session.AdvanceSimulation(SurvivalRecovery.MaximumRestHours * Hour));

                Assert.That(recovery.LastRestStop, Is.EqualTo(expected));
                Assert.That(recovery.RestedSimulationSeconds, Is.EqualTo(3d * Hour).Within(1e-3), "stopped at the depletion, not at the planned end");
                Assert.That(session.Resources.GetQuantity(store), Is.EqualTo(0d).Within(1e-9));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "an empty store wakes the crew; it is not a loss by itself");
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
                Assert.That(recovery.TryBeginRest(1d).Failure, Is.EqualTo(CommandFailure.InsufficientResources), "no rest on an empty store");
            }
        }

        [Test]
        public void ARest_StopsWhenTheStoredOxygenIsUsedUp_BeforeTheCabinAirRunsOut()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                double oxygenPerSecond = session.Resources.Rates.OxygenUseKgPerSecond;
                Assert.That(oxygenPerSecond, Is.GreaterThan(0d));
                SpendDown(session, ResourceId.OxygenKg, oxygenPerSecond * 2d * Hour);

                Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(session.AdvanceSimulation(2d * Hour));

                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.OxygenStoreEmpty));
                Assert.That(recovery.RestedSimulationSeconds, Is.EqualTo(2d * Hour).Within(1e-3));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "the crew wakes while the cabin air still holds");
                Assert.That(recovery.CheckRest(1d).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            }
        }

        [Test]
        public void ADangerWinsATieWithThePlannedEnd()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                SpendDown(session, ResourceId.FoodKg, FoodPerSecond(session) * 4d * Hour);

                Require(recovery.TryBeginRest(4d));
                Require(session.AdvanceSimulation(4d * Hour));

                Assert.That(recovery.IsResting, Is.False);
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.FoodStoreEmpty));
            }
        }

        [Test]
        public void ChainedRests_CarryTheCrewThroughTheCropCycle_AndTheHarvestWakesThem()
        {
            using (MissionSession session = Contact(extraKit: true))
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                Require(session.Survival.TryBuildGrowModule());
                Require(session.Survival.TryPlant());
                double growth = session.Survival.CropRemainingSimulationSeconds;
                double foodBefore = session.Resources.Snapshot.FoodKg;

                int rests = 0;
                while (session.Survival.GrowStage == GrowModuleStage.Growing && rests < 60)
                {
                    if (recovery.Stage == RecoveryStage.BeaconRequested)
                    {
                        Require(recovery.TryPowerBeacon());
                    }

                    RestUntilStop(session);
                    rests++;
                }

                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.HarvestReady));
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.CropReady), "the finished crop wakes the crew");
                Assert.That(recovery.BeaconPoweredAtSimulationSeconds, Is.Not.Null, "the day-6 beacon request woke the crew once");
                Assert.That(recovery.RestedSimulationSeconds, Is.EqualTo(growth).Within(Hour), "nearly the whole cycle was spent resting");
                Assert.That(rests, Is.LessThanOrEqualTo(26), "about 24 rests of 8.5 real seconds, not an endless wait");
                Assert.That(foodBefore - session.Resources.Snapshot.FoodKg, Is.EqualTo(FoodPerSecond(session) * recovery.RestedSimulationSeconds).Within(1e-6),
                    "food is drawn only while resting");
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Require(session.Survival.TryHarvest());
            }
        }

        [Test]
        public void OneLongRest_EqualsManySmallTicks()
        {
            using (MissionSession whole = Contact())
            using (MissionSession pieces = Contact())
            {
                Require(whole.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(pieces.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));

                Require(whole.AdvanceSimulation(SurvivalRecovery.MaximumRestHours * Hour));
                for (int i = 0; i < 85; i++)
                {
                    Require(pieces.Tick(0.1d));
                }

                Assert.That(pieces.State.ElapsedSimulationSeconds, Is.EqualTo(whole.State.ElapsedSimulationSeconds).Within(1e-6));
                Fixtures.AssertSnapshot(whole.Resources.Snapshot, pieces.Resources.Snapshot, 1e-9);
                Assert.That(whole.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                Assert.That(pieces.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                Assert.That(pieces.SurvivalRecovery.RestedSimulationSeconds, Is.EqualTo(whole.SurvivalRecovery.RestedSimulationSeconds).Within(1e-6));
                Assert.That(pieces.Survival.Hunger01, Is.EqualTo(whole.Survival.Hunger01).Within(1e-9));
            }
        }

        [Test]
        public void PauseFreezesTheRest_WakeWorksOnce_AndNoRestOutlivesTheMission()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                int changes = 0;
                recovery.Changed += () => changes++;
                Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(session.Tick(1d));
                double remaining = recovery.RestRemainingSimulationSeconds;
                Assert.That(remaining, Is.EqualTo(7.5d * Hour).Within(1e-3));

                Require(session.Clock.Pause());
                session.Tick(5d);
                Assert.That(session.AdvanceSimulation(Hour).Succeeded, Is.False);
                Assert.That(recovery.RestRemainingSimulationSeconds, Is.EqualTo(remaining), "paused: the rest is frozen");
                Assert.That(recovery.TryWake().Succeeded, Is.False, "resume before waking");
                Require(session.Clock.Resume());

                Require(recovery.TryWake());
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Woken));
                Assert.That(session.Clock.TimeScale, Is.EqualTo(session.Survival.Spec.TimeScale));
                int entries = session.Director.Journal.Snapshot().Count;
                Assert.That(recovery.TryWake().Succeeded, Is.False);
                Assert.That(session.Director.Journal.Snapshot().Count, Is.EqualTo(entries), "a second wake records nothing");
                Assert.That(changes, Is.EqualTo(2));

                Require(recovery.TryBeginRest(2d));
                Require(session.State.TryEndMission(MissionOutcome.Lost, "Test ending while the crew rests.", session.Resources.Snapshot));
                Assert.That(recovery.IsResting, Is.False, "an ended mission leaves no rest lease");
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.MissionEnded));
            }

            MissionSession disposed = Contact();
            SurvivalRecovery leftover = disposed.SurvivalRecovery;
            Require(leftover.TryBeginRest(2d));
            disposed.Dispose();
            Assert.That(leftover.IsResting, Is.False, "a disposed session leaves no rest lease");
        }

        [Test]
        public void Contact_StartsOneFictionalRecoveryPlan_TwelveDaysOut_WithABeaconRequestOnDaySix()
        {
            const double Day = CrewMetabolicAssumptions.SecondsPerDay;
            using (MissionSession session = Contact(receive: false))
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.NotStarted), "a sent call plans nothing yet");
                Assert.That(recovery.ArrivalAtSimulationSeconds, Is.Null);
                Assert.That(recovery.ArrivalRemainingSimulationSeconds, Is.EqualTo(double.PositiveInfinity));
                Assert.That(recovery.CheckPowerBeacon().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));

                double contact = recovery.ContactAtSimulationSeconds.Value;
                Assert.That(contact, Is.EqualTo(session.Survival.ContactRestoredAtSimulationSeconds.Value));
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.Inbound));
                Assert.That(recovery.ArrivalAtSimulationSeconds, Is.EqualTo(contact + (12d * Day)).Within(1e-6));
                Assert.That(recovery.ArrivalRemainingSimulationSeconds, Is.EqualTo(12d * Day).Within(1e-6));
                Assert.That(recovery.BeaconWindowEndsAtSimulationSeconds, Is.Null);
                Assert.That(recovery.CheckPowerBeacon().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "not requested yet");
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "no rescue on mere radio contact");
                Assert.That(JournalCount(session, "fictional recovery craft"), Is.EqualTo(1));
            }
        }

        [Test]
        public void ADefaultLoadout_RestsToTheBeacon_PowersItOnce_AndIsRecoveredOnSchedule_ThenTheReportOpens()
        {
            const double Day = CrewMetabolicAssumptions.SecondsPerDay;
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                double contact = recovery.ContactAtSimulationSeconds.Value;
                for (int guard = 0; recovery.Stage == RecoveryStage.Inbound; guard++)
                {
                    Assert.That(guard, Is.LessThan(40));
                    RestUntilStop(session);
                }

                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested));
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.RecoveryMilestone), "the request wakes the crew");
                Assert.That(recovery.BeaconWindowEndsAtSimulationSeconds, Is.EqualTo(contact + (6d * Day) + (2d * Hour)).Within(1e-6));

                IInteractable console = Get(session, InteractionIds.CockpitConsole);
                Assert.That(console.Prompt, Does.Contain("beacon"));
                Assert.That(console.CanInteract, Is.True);
                double battery = session.Resources.Snapshot.BatteryKWh;
                Require(Get(session, InteractionIds.RecoveryBeacon).TryInteract());
                Assert.That(battery - session.Resources.Snapshot.BatteryKWh, Is.EqualTo(SurvivalRecovery.BeaconEnergyKWh).Within(1e-9), "real energy");
                Assert.That(recovery.TryPowerBeacon().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(console.TryInteract().Succeeded, Is.False, "the console does not power it twice");
                Assert.That(battery - session.Resources.Snapshot.BatteryKWh, Is.EqualTo(SurvivalRecovery.BeaconEnergyKWh).Within(1e-9), "charged once");
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.Inbound));
                Assert.That(recovery.ArrivalAtSimulationSeconds, Is.EqualTo(contact + (12d * Day)).Within(1e-6), "on schedule");

                for (int guard = 0; session.State.IsSimulationRunning; guard++)
                {
                    Assert.That(guard, Is.LessThan(40));
                    RestUntilStop(session);
                }

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered), session.State.OutcomeReason);
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.None));
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.Arrived));
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.MissionEnded));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(contact + (12d * Day)).Within(1e-3));
                Assert.That(session.State.TryEndMission(MissionOutcome.Lost, "Late test ending.", session.Resources.Snapshot).Succeeded, Is.False);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered), "no duplicate outcome");
                Assert.That(JournalCount(session, "Mission outcome"), Is.EqualTo(1));

                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.RecoveryArrival));
                Fixtures.AcknowledgeActiveCinematic(session);
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.MarsDeparture), "Mars is its own later sequence");
                Fixtures.AcknowledgeActiveCinematic(session);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report));

                MissionReport report = session.Report;
                Assert.That(report.Outcome, Is.EqualTo(MissionOutcome.Recovered));
                Assert.That(report.Headline, Is.EqualTo("RECOVERED"));
                Assert.That(Value(report, MissionReportSections.Outcome, "What it means"), Does.Contain("fictional").And.Contain("Mars"));
                Assert.That(Value(report, MissionReportSections.Resources, "Rest"), Does.Contain("BVAD"));
                Assert.That(Value(report, MissionReportSections.Resources, "Recovery beacon"), Does.StartWith("Powered"));
            }
        }

        [Test]
        public void AMissedBeaconWindow_DelaysTheRendezvousOnce_PausedTimeUsesNoWindow_AndTheTimeoutReachesTheReport()
        {
            const double Day = CrewMetabolicAssumptions.SecondsPerDay;
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                double contact = recovery.ContactAtSimulationSeconds.Value;
                for (int guard = 0; recovery.Stage == RecoveryStage.Inbound; guard++)
                {
                    Assert.That(guard, Is.LessThan(40));
                    RestUntilStop(session);
                }

                double windowEnds = recovery.BeaconWindowEndsAtSimulationSeconds.Value;
                Require(session.Clock.Pause());
                session.Tick(600d);
                Require(session.Clock.Resume());
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested), "paused time uses none of the window");
                Assert.That(recovery.BeaconWindowEndsAtSimulationSeconds, Is.EqualTo(windowEnds));

                Require(session.AdvanceSimulation(windowEnds - session.State.ElapsedSimulationSeconds + 60d));
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.Delayed));
                Assert.That(recovery.BeaconMissed, Is.True);
                Assert.That(recovery.ArrivalAtSimulationSeconds, Is.EqualTo(contact + (15d * Day)).Within(1e-6), "one explicit 3-day delay");
                Assert.That(recovery.BeaconWindowEndsAtSimulationSeconds, Is.Null);
                double battery = session.Resources.Snapshot.BatteryKWh;
                Assert.That(recovery.TryPowerBeacon().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "too late");
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery), "a refused beacon costs nothing");
                Require(session.AdvanceSimulation(Hour));
                Assert.That(recovery.ArrivalAtSimulationSeconds, Is.EqualTo(contact + (15d * Day)).Within(1e-6), "not delayed again");
                Assert.That(JournalCount(session, "Beacon window missed"), Is.EqualTo(1));

                for (int guard = 0; session.State.IsSimulationRunning; guard++)
                {
                    Assert.That(guard, Is.LessThan(60));
                    RestUntilStop(session);
                }

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered), session.State.OutcomeReason);
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(contact + (15d * Day)).Within(1e-3));

                double timeout = session.Definition.Scenario.EndingCinematicTimeoutRealSeconds;
                session.Tick(timeout + 1d);
                session.Tick(timeout + 1d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report), "the bounded sequence reaches the report without acknowledgements");
                Assert.That(Value(session.Report, MissionReportSections.Resources, "Recovery beacon"), Does.StartWith("Missed"));
            }
        }

        [Test]
        public void AWaterShortage_LosesTheCrewBeforeTheRecoveryCraftArrives()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                SpendDown(session, ResourceId.WaterLitres, WaterPerSecond(session) * 2d * CrewMetabolicAssumptions.SecondsPerDay);
                for (int guard = 0; recovery.LastRestStop != RestStopReason.WaterStoreEmpty; guard++)
                {
                    Assert.That(guard, Is.LessThan(20));
                    RestUntilStop(session);
                }

                Assert.That(recovery.CheckRest(1d).Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Assert.That(session.Survival.CheckDrinkWater().Succeeded, Is.False);
                session.AdvanceSimulation(session.Survival.Spec.ThirstLimitSeconds);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost), session.State.OutcomeReason);
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.ThirstLimit));
                Assert.That(recovery.Stage, Is.Not.EqualTo(RecoveryStage.Arrived), "no rescue for a lost crew");
                Assert.That(recovery.RecoveredAtSimulationSeconds, Is.Null);
            }
        }

        [Test]
        public void ALossAtTheSameInstantAsTheRendezvous_Wins()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                for (int guard = 0; recovery.Stage == RecoveryStage.Inbound; guard++)
                {
                    Assert.That(guard, Is.LessThan(40));
                    RestUntilStop(session);
                }

                Require(recovery.TryPowerBeacon());
                double ThirstLeft() => session.Survival.Spec.ThirstLimitSeconds * (1d - session.Survival.Thirst01);
                double gap = recovery.ArrivalRemainingSimulationSeconds - ThirstLeft();
                Assert.That(gap, Is.GreaterThan(0d));
                for (int guard = 0; gap > 1e-6; guard++)
                {
                    Assert.That(guard, Is.LessThan(40));
                    double rest = Math.Min(gap, SurvivalRecovery.MaximumRestHours * Hour);
                    Require(recovery.TryBeginRest(rest / Hour));
                    Require(session.AdvanceSimulation(rest));
                    Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                    gap = recovery.ArrivalRemainingSimulationSeconds - ThirstLeft();
                }

                // Awake and not drinking, the thirst limit now falls on the rendezvous instant.
                session.AdvanceSimulation(recovery.ArrivalRemainingSimulationSeconds);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost), "the loss wins the tie");
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.ThirstLimit));
                Assert.That(recovery.Stage, Is.Not.EqualTo(RecoveryStage.Arrived));
            }
        }

        // P1 6881113735: one long frame must advance exactly as far as many short ones when a step changes the scale.
        [Test]
        public void AFrameThatEndsARestMidway_SpendsItsRemainderAtTheAwakeScale_LikeManySmallFrames()
        {
            using (MissionSession one = Contact())
            using (MissionSession ten = Contact())
            {
                double start = one.State.ElapsedSimulationSeconds;
                Assert.That(ten.State.ElapsedSimulationSeconds, Is.EqualTo(start).Within(1e-9));
                Require(one.SurvivalRecovery.TryBeginRest(0.01d));
                Require(ten.SurvivalRecovery.TryBeginRest(0.01d));

                Require(one.Tick(0.1d));
                for (int i = 0; i < 10; i++)
                {
                    Require(ten.Tick(0.01d));
                }

                // 36 s of rest take 0.01 real s at 3,600; the remaining 0.09 real s run awake at the Survival scale.
                double expected = 36d + (0.09d * one.Survival.Spec.TimeScale);
                Assert.That(one.State.ElapsedSimulationSeconds - start, Is.EqualTo(expected).Within(1e-6), "one frame");
                Assert.That(ten.State.ElapsedSimulationSeconds - start, Is.EqualTo(expected).Within(1e-6), "ten frames");
                Assert.That(one.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                Assert.That(ten.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                Assert.That(one.Clock.TimeScale, Is.EqualTo(one.Survival.Spec.TimeScale));
                Fixtures.AssertSnapshot(one.Resources.Snapshot, ten.Resources.Snapshot, 1e-9);
                Assert.That(one.Survival.Thirst01, Is.EqualTo(ten.Survival.Thirst01).Within(1e-12));
            }
        }

        [Test]
        public void AFrameThatWakesTheCrewAtADanger_DoesNotRunOnAtTheRestScaleIntoALoss()
        {
            using (MissionSession one = Contact())
            using (MissionSession ten = Contact())
            {
                foreach (MissionSession session in new[] { one, ten })
                {
                    // Codex's reproduction: 60 simulated seconds short of the thirst limit, food for 36 s of rest.
                    Require(session.AdvanceSimulation(session.Survival.Spec.ThirstLimitSeconds - 60d));
                    SpendDown(session, ResourceId.FoodKg, FoodPerSecond(session) * 36d);
                    Require(session.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                }

                double start = one.State.ElapsedSimulationSeconds;
                Require(one.Tick(0.1d));
                for (int i = 0; i < 10; i++)
                {
                    Require(ten.Tick(0.01d));
                }

                foreach (MissionSession session in new[] { one, ten })
                {
                    Assert.That(session.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.FoodStoreEmpty));
                    Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "awake at 300 the crew still has 33 s before the thirst limit");
                    Assert.That(session.State.ElapsedSimulationSeconds - start, Is.EqualTo(36d + (0.09d * session.Survival.Spec.TimeScale)).Within(1e-6));
                }
            }
        }

        [Test]
        public void AFrameAcrossContact_SpendsItsRemainderAtTheSurvivalScale()
        {
            using (MissionSession one = Contact(receive: false))
            using (MissionSession ten = Contact(receive: false))
            {
                double start = one.State.ElapsedSimulationSeconds;
                double transmissionScale = one.Clock.TimeScale;
                double reply = one.Communications.ReplyRemainingSimulationSeconds;
                double frame = (reply / transmissionScale) + 0.5d;

                Require(one.Tick(frame));
                for (int i = 0; i < 10; i++)
                {
                    Require(ten.Tick(frame / 10d));
                }

                foreach (MissionSession session in new[] { one, ten })
                {
                    Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
                    Assert.That(session.State.ElapsedSimulationSeconds - start, Is.EqualTo(reply + (0.5d * session.Survival.Spec.TimeScale)).Within(1e-3),
                        "the half real second after contact runs at the Survival scale");
                }
            }
        }
    }
}
