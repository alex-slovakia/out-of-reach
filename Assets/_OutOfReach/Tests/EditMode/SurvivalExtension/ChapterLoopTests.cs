using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>
    /// CORE-CHAPTER-LOOP: the approved day 1-30 loop (docs/APPROVED-CHAPTER-LOOP-2026-11-15.md) played through the
    /// session's own frame driver. Routine segments advance the one clock and ledger; Core issues the real ration,
    /// water and rest commands; every stop keeps its actual time; nothing is granted at a boundary.
    /// </summary>
    public sealed class ChapterLoopTests
    {
        private const double Hour = 3600d;
        private const double Day = CrewMetabolicAssumptions.SecondsPerDay;
        private const double Frame = 0.1d;
        private const double Tolerance = 1e-6;

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        /// <summary>Launched with the chapter loop and the payload salvage bound, impact acknowledged: day 1, Emergency.</summary>
        private static MissionSession Accident(bool salvage = true, string presetId = null)
        {
            var session = new MissionSession(startingPresetId: presetId, survivalExtension: true, chapterLoop: true);
            session.EnableFieldModifications(salvage, false);
            Require(session.Planner.TryLaunch());
            Fixtures.TriggerImpact(session);
            return session;
        }

        /// <summary>Repairs, the first meal and water, and one ordinary rest (the day-1 beat needs only the repairs and the meal).</summary>
        private static void CompleteAccidentBeat(MissionSession session)
        {
            Fixtures.RepairOrFail(session, ShipSystemId.Power);
            Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
            double due = session.Survival.Spec.PortionSeconds - (session.State.ElapsedSimulationSeconds - session.Survival.ActivatedAtSimulationSeconds.Value);
            if (due > 0d)
            {
                Require(session.AdvanceSimulation(due));
            }

            Require(session.Survival.TryConsumeRation());
            Require(session.Survival.TryDrinkWater());
            Require(session.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
            Require(session.AdvanceSimulation(SurvivalRecovery.MaximumRestHours * Hour));
            Assert.That(session.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
        }

        private sealed class Segment
        {
            public ResourceSnapshot Before;
            public ResourceSnapshot After;
            public double StartedAt;
            public double RestedBefore;
            public double RestedAfter;
            public int Frames;
            public int Rations;
        }

        /// <summary>
        /// Starts a routine and drives real frames until it stops. Every frame: no store may grow, no rest may exceed
        /// 8.5 h, player control is off, and the hunger/thirst stay below their limits unless a store is empty.
        /// </summary>
        private static Segment RunRoutine(MissionSession session)
        {
            MissionChapterFlow flow = session.ChapterFlow;
            Segment total = RunSegment(session, true);
            int steps = 1;
            while (flow.LastStopWasStep)
            {
                // CHAPTER-PACING-01: after the day-14 news a routine runs in steps of at most 3 days up to the next beat.
                Assert.That(flow.Rescue, Is.EqualTo(RescuePlan.InTransitCraftCommitted), "3-day steps only after the news");
                Assert.That(steps++, Is.LessThan(12), "the steps end at the beat");
                Segment next = RunSegment(session, true);
                total = new Segment
                {
                    Before = total.Before, After = next.After, StartedAt = total.StartedAt, RestedBefore = total.RestedBefore,
                    RestedAfter = next.RestedAfter, Frames = total.Frames + next.Frames, Rations = total.Rations + next.Rations,
                };
            }

            return total;
        }

        /// <summary>One routine segment in real frames (<paramref name="start"/> false: already started, e.g. by the berth).</summary>
        private static Segment RunSegment(MissionSession session, bool start)
        {
            MissionChapterFlow flow = session.ChapterFlow;
            var segment = new Segment
            {
                Before = session.Resources.Snapshot,
                StartedAt = flow.RoutineStartedAtSimulationSeconds ?? session.State.ElapsedSimulationSeconds,
                RestedBefore = session.SurvivalRecovery.RestedSimulationSeconds,
            };
            if (start)
            {
                segment.StartedAt = session.State.ElapsedSimulationSeconds;
                Require(flow.TryContinueRoutine());
            }

            ResourceSnapshot previous = session.Resources.Snapshot;
            while (flow.IsRoutineRunning)
            {
                Assert.That(segment.Frames++, Is.LessThan(20000), "the routine stops");
                Assert.That(session.IsPlayerControlAllowed, Is.False, "no player control during a routine");
                Assert.That(session.Clock.TimeScale, Is.EqualTo(MissionChapterFlow.RoutineTimeScale));
                Require(session.Tick(Frame));
                ResourceSnapshot now = session.Resources.Snapshot;
                Assert.That(now.FoodKg, Is.LessThanOrEqualTo(previous.FoodKg + Tolerance), "no free food");
                Assert.That(now.WaterLitres, Is.LessThanOrEqualTo(previous.WaterLitres + Tolerance), "no free water");
                Assert.That(now.OxygenKg, Is.LessThanOrEqualTo(previous.OxygenKg + Tolerance), "no free oxygen");
                Assert.That(now.SparePartsCount, Is.LessThanOrEqualTo(previous.SparePartsCount), "no free parts");
                Assert.That(session.SurvivalRecovery.RestRemainingSimulationSeconds, Is.LessThanOrEqualTo(SurvivalRecovery.MaximumRestHours * Hour + Tolerance));
                previous = now;
            }

            segment.After = session.Resources.Snapshot;
            segment.RestedAfter = session.SurvivalRecovery.RestedSimulationSeconds;
            segment.Rations = flow.RoutineRations;
            Assert.That(session.Clock.TimeScale, Is.Not.EqualTo(MissionChapterFlow.RoutineTimeScale), "the routine scale ends with the routine");
            if (flow.LastStopWasStep)
            {
                Assert.That(flow.RoutineStoppedAtSimulationSeconds.Value - flow.RoutineStartedAtSimulationSeconds.Value,
                    Is.LessThanOrEqualTo((MissionChapterFlow.MaximumStepDaysAfterNews * Day) + Tolerance), "a step covers at most 3 days");
            }

            return segment;
        }

        /// <summary>Food: every ration Core issued plus the BVAD draw for the time actually rested, and nothing else.</summary>
        private static void AssertFoodReconciles(MissionSession session, Segment segment)
        {
            double rested = segment.RestedAfter - segment.RestedBefore;
            double expected = (segment.Rations * session.Survival.RationFoodKg)
                + (rested * CrewMetabolicAssumptions.FoodKgPerCrewDay * session.Definition.CrewCount / Day);
            Assert.That(segment.Before.FoodKg - segment.After.FoodKg, Is.EqualTo(expected).Within(1e-6), "food = rations + rest draw");
        }

        private static void ReachContact(MissionSession session)
        {
            Fixtures.CompleteEva(session);
            Fixtures.Transmit(session);
            Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
        }

        /// <summary>Sent after Earth's reply reached the ship, received one packaged light time later (or later), never before.</summary>
        private static void AssertCausalDispatch(MissionSession session, MissionDispatch dispatch)
        {
            double sent = dispatch.TransmittedAtSimulationSeconds;
            double received = dispatch.ReceivedAtSimulationSeconds;
            Assert.That(sent, Is.GreaterThanOrEqualTo(session.Communications.ReplyReceivedAtSimulationSeconds.Value), "sent after the rescue was planned");
            EarthMarsReference reference = EarthMarsReference.Embedded;
            Assert.That(reference.TrySelect(session.Calendar.EpochAtStartUtc.AddSeconds(sent), out EarthMarsSample sample, out string problem), Is.True, problem);
            Assert.That(received - sent, Is.GreaterThanOrEqualTo(sample.OneWayLightSeconds - 1e-6), "no faster than light");
            Assert.That(sample.OneWayLightSeconds, Is.GreaterThan(60d));
        }

        [Test]
        public void TheLegacySession_HasNoChapterFlow_AndTheChapterLoopNeedsTheSurvivalExtension()
        {
            using (var plain = new MissionSession(survivalExtension: true))
            {
                Assert.That(plain.ChapterFlow, Is.Null);
            }

            Assert.Throws<ArgumentException>(() => new MissionSession(chapterLoop: true));
        }

        [Test]
        public void Day1_PortionAccountingStartsOnceWhenTheImpactEnds_AndTheFirstPaidMealIsEarlyOnce_WithoutADoubleCharge()
        {
            using (MissionSession session = Accident())
            {
                SurvivalExtension survival = session.Survival;
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));
                Assert.That(survival.IsActive, Is.True, "needs run from day 1 in chapter mode");
                Assert.That(survival.ActivatedAtSimulationSeconds, Is.EqualTo(session.State.ElapsedSimulationSeconds));
                Assert.That(survival.ContactRestoredAtSimulationSeconds, Is.Null, "activation is not contact");
                Assert.That(session.Resources.Rates.FoodUseKgPerSecond, Is.EqualTo(0d), "no continuous food draw while awake");
                Assert.That(session.Resources.Rates.WaterUseLitresPerSecond, Is.EqualTo(0d), "no continuous water draw while awake");
                Assert.That(session.ChapterFlow.Chapter, Is.EqualTo(MissionChapter.Accident), session.ChapterFlow.LastRoutineStop + ": " + session.ChapterFlow.LastRoutineStopText);

                double food = session.Resources.Snapshot.FoodKg;
                Assert.That(survival.TryDrinkWater().Succeeded, Is.False, "water keeps its 6 h portion gate");
                Require(survival.TryConsumeRation());
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(food - survival.RationFoodKg).Within(1e-9), "the first meal is paid in full");
                Assert.That(survival.TryConsumeRation().Succeeded, Is.False, "only the first meal skips the gate; a repeated press charges nothing");
                Assert.That(survival.Hunger01, Is.EqualTo(0d));

                // Its unused part is a satiation credit: the next ration is due 12 h after the impact, not 6 h.
                Require(session.AdvanceSimulation((2d * survival.Spec.PortionSeconds) - 60d));
                Assert.That(survival.CheckConsumeRation().Succeeded, Is.False, "still covered by the first meal");
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(food - survival.RationFoodKg).Within(1e-12), "time alone draws no food");
                Require(session.AdvanceSimulation(60d));
                Require(survival.TryConsumeRation());
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(food - (2d * survival.RationFoodKg)).Within(1e-9));
                Assert.That(survival.RationsEaten, Is.EqualTo(2));
            }
        }

        [Test]
        public void Day1_RoutineIsRefusedUntilRepairsAndTheFirstMeal_AndEvaIsNotDateGated()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                Assert.That(flow.CheckContinueRoutine().Explanation, Does.Contain("power and life support"));
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Assert.That(flow.CheckContinueRoutine().Explanation, Does.Contain("ration"));
                Assert.That(session.Eva.CheckPrepareSuit().Succeeded, Is.True, "no date-only EVA gate: early exterior work is allowed");

                double before = session.State.ElapsedSimulationSeconds;
                Assert.That(flow.TryContinueRoutine().Succeeded, Is.False);
                Assert.That(flow.TrySleepInteraction().Succeeded, Is.False, "the berth explains what is missing");
                Assert.That(flow.IsRoutineRunning, Is.False);
                Assert.That(session.SurvivalRecovery.IsResting, Is.False);
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(before), "a refusal changes nothing");
                Require(session.Survival.TryConsumeRation());
                Assert.That(flow.CheckContinueRoutine().Succeeded, Is.True, "no completed rest is needed: the first night is the story sleep");
            }
        }

        [Test]
        public void TheWholeLoop_Day1To30_ReachesEveryBeat_WithReconciledReserves_AndTheRecovery()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                SurvivalRecovery recovery = session.SurvivalRecovery;
                CompleteAccidentBeat(session);

                // Days 2 -> 5: three days of life, not one long sleep.
                int journalBefore = session.Director.Journal.Count;
                Segment toDay5 = RunRoutine(session);
                Assert.That(session.Director.Journal.Count - journalBefore, Is.LessThanOrEqualTo(3),
                    "a routine segment adds its begin, its summary and the beat line to the report timeline, not one line per meal or rest");
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.ReachedChapter), flow.LastRoutineStopText);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Consequences), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                Assert.That(flow.RoutineStoppedAtSimulationSeconds.Value, Is.EqualTo(MissionChapterFlow.BeatStart(MissionChapter.Consequences)).Within(Tolerance));
                Assert.That(recovery.MissionDay, Is.EqualTo(5));
                Assert.That(flow.RoutineRests, Is.InRange(3, 4), "an ordinary rest each night");
                Assert.That(flow.RoutineRations, Is.GreaterThanOrEqualTo(6), "rations each day");
                Assert.That(flow.RoutineDrinks, Is.GreaterThanOrEqualTo(6), "water each day");
                AssertFoodReconciles(session, toDay5);
                Assert.That(session.Clock.TimeScale, Is.EqualTo(session.Definition.Scenario.Pacing.InteriorTimeScale), "the chapter's own speed returns");
                Assert.That(recovery.IsResting, Is.False, "the crew is awake at the beat");

                // Day 5: the decision gates the routine; a wrong or duplicate choice changes nothing.
                Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.ScienceOrSalvage));
                Assert.That(flow.TryContinueRoutine().Succeeded, Is.False);
                Assert.That(flow.TryChoose(ChapterChoice.KeepGrowRunning).Succeeded, Is.False);
                Require(flow.TryChoose(ChapterChoice.PreserveScience));
                Assert.That(flow.TryChoose(ChapterChoice.PreserveScience).Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(session.FieldModifications.SciencePayloadSalvaged, Is.False);

                // Days 5 -> 9, then the exterior repair and the causal call.
                RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Contact), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                Assert.That(recovery.MissionDay, Is.EqualTo(9));
                Assert.That(session.Eva.CheckPrepareSuit().Succeeded, Is.True, session.Eva.CheckPrepareSuit().Explanation);
                Assert.That(flow.CheckContinueRoutine().Succeeded, Is.False, "contact first");
                ReachContact(session);
                Assert.That(flow.Rescue, Is.EqualTo(RescuePlan.FirstRescuePlanned));
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.NotStarted), "no legacy +12-day plan");

                // Days 9 -> 14: the dispatch arrives at the beat and left Earth one light time earlier.
                RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.RescueFailed), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                MissionDispatch dispatch = flow.LatestDispatch;
                Assert.That(dispatch.Id, Is.EqualTo(MissionChapterFlow.RescueFailedDispatchId));
                AssertCausalDispatch(session, dispatch);
                Assert.That(dispatch.ReceivedAtSimulationSeconds, Is.EqualTo(MissionChapterFlow.BeatStart(MissionChapter.RescueFailed)).Within(120d));
                Assert.That(flow.ChapterStartedAt[MissionChapter.RescueFailed], Is.EqualTo(dispatch.ReceivedAtSimulationSeconds), "the beat is the news");
                Assert.That(flow.Rescue, Is.EqualTo(RescuePlan.FirstRescueFailed));
                Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.AcknowledgeRescueFailure));
                Require(flow.TryChoose(ChapterChoice.AcknowledgeRescueFailure));
                Assert.That(flow.Rescue, Is.EqualTo(RescuePlan.InTransitCraftCommitted));
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.Inbound));
                Assert.That(MissionCalendar.DayNumberAt(recovery.ArrivalAtSimulationSeconds.Value), Is.EqualTo(30));

                // Days 14 -> 20: final decision.
                RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.FinalDecision), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.GrowOrBeacon));
                Assert.That(flow.TryChoose(ChapterChoice.StopGrowForBeacon).Succeeded, Is.False, "nothing grows");
                Require(flow.TryChoose(ChapterChoice.KeepGrowRunning));

                // Days 20 -> 30: the craft asks for the beacon at the rendezvous beat.
                Segment toDay30 = RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Rendezvous), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested));
                Assert.That(recovery.MissionDay, Is.EqualTo(30));
                AssertFoodReconciles(session, toDay30);
                Assert.That(flow.TryContinueRoutine().Succeeded, Is.False, "answer the beacon request first");
                Require(recovery.TryPowerBeacon());

                RunRoutine(session);
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.MissionEnded));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered));
                Assert.That(recovery.RecoveredAtSimulationSeconds.Value, Is.EqualTo(recovery.ArrivalAtSimulationSeconds.Value).Within(Tolerance));
                Assert.That(MissionCalendar.DayNumberAt(recovery.RecoveredAtSimulationSeconds.Value), Is.EqualTo(30));
                Assert.That(session.Survival.RationsEaten, Is.GreaterThan(50), "about four portions per day, all real commands");
            }
        }

        [Test]
        public void Day5_TheExistingSalvageInteractionSettlesTheDecision_WithItsOwnCreditOnly()
        {
            using (MissionSession session = Accident())
            {
                CompleteAccidentBeat(session);
                RunRoutine(session);
                MissionChapterFlow flow = session.ChapterFlow;
                Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.ScienceOrSalvage));
                int parts = session.Resources.Snapshot.SparePartsCount;
                int credit = session.FieldModifications.SalvageablePartsNow;
                Assert.That(credit, Is.GreaterThan(0));
                Assert.That(session.Interactions.TryGet(InteractionIds.SalvagePayload, out IInteractable salvage), Is.True);
                Require(salvage.TryInteract());
                Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.None));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(parts + credit));
                Assert.That(flow.TryChoose(ChapterChoice.PreserveScience).Succeeded, Is.False, "already decided");
                Assert.That(flow.CheckContinueRoutine().Succeeded, Is.True, flow.CheckContinueRoutine().Explanation);
            }
        }

        private static IInteractable Interaction(MissionSession session, string id)
        {
            Assert.That(session.Interactions.TryGet(id, out IInteractable interactable), Is.True, id);
            return interactable;
        }

        private static bool JournalContains(MissionSession session, string text)
        {
            foreach (MissionLogEntry entry in session.Director.Journal.Snapshot())
            {
                if (entry.Text.Contains(text))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The open GrowOrConserve decision gates the routine and refuses every other choice, changing nothing.</summary>
        private static void AssertGrowOrConserveOpen(MissionSession session)
        {
            MissionChapterFlow flow = session.ChapterFlow;
            Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Consequences), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
            Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.GrowOrConserve), "never silently completed");
            Assert.That(flow.ConsequencesResolution, Is.Null);
            ResourceSnapshot before = session.Resources.Snapshot;
            CommandResult routine = flow.TryContinueRoutine();
            Assert.That(routine.Succeeded, Is.False);
            Assert.That(routine.Explanation, Does.Contain("grow module"));
            Assert.That(flow.TryChoose(ChapterChoice.PreserveScience).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "no instrument to preserve");
            Assert.That(flow.TryChoose(ChapterChoice.KeepGrowRunning).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(flow.TryChoose(ChapterChoice.AcknowledgeRescueFailure).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(flow.CheckChoose(ChapterChoice.ConserveReserves).Succeeded, Is.True, "conserving is always possible");
            Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);
            Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.GrowOrConserve));
        }

        /// <summary>The existing paid build target settles the decision; the ledger changes by exactly its cost, once.</summary>
        private static void BuildThroughTheExistingTarget(MissionSession session)
        {
            MissionChapterFlow flow = session.ChapterFlow;
            IInteractable build = Interaction(session, InteractionIds.BuildGrowModule);
            Assert.That(build.CanInteract, Is.True, build.Prompt);
            ResourceSnapshot before = session.Resources.Snapshot;
            CommandResult built = build.TryInteract();
            Require(built);
            ResourceSnapshot after = session.Resources.Snapshot;
            Assert.That(after.SparePartsCount, Is.EqualTo(before.SparePartsCount - session.Survival.Spec.BuildSpareParts), "the build's own part cost");
            Assert.That(after.BatteryKWh, Is.EqualTo(before.BatteryKWh - session.Survival.Spec.BuildEnergyKWh).Within(1e-9), "the build's own energy cost");
            Assert.That(after.FoodKg, Is.EqualTo(before.FoodKg));
            Assert.That(after.WaterLitres, Is.EqualTo(before.WaterLitres));
            Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.ReadyToPlant));
            Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.None));
            Assert.That(flow.ConsequencesResolution, Is.EqualTo("Grow module built"));
            Assert.That(JournalContains(session, "Day-5 decision: the crew invests"), Is.True);
            Assert.That(build.TryInteract().Succeeded, Is.False, "a repeated press charges nothing");
            Fixtures.AssertSnapshot(after, session.Resources.Snapshot, 0d);
            Assert.That(flow.TryChoose(ChapterChoice.ConserveReserves).Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
            Assert.That(flow.CheckContinueRoutine().Succeeded, Is.True, flow.CheckContinueRoutine().Explanation);
        }

        private static void ConserveReserves(MissionSession session)
        {
            MissionChapterFlow flow = session.ChapterFlow;
            ResourceSnapshot before = session.Resources.Snapshot;
            CommandResult check = flow.CheckChoose(ChapterChoice.ConserveReserves);
            Require(check);
            Assert.That(check.Explanation, Does.Contain("spare part"), "the forecast names the kept reserves");
            Require(flow.TryChoose(ChapterChoice.ConserveReserves));
            Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);
            Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Unbuilt));
            Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.None));
            Assert.That(flow.ConsequencesResolution, Is.EqualTo("Reserves conserved"));
            Assert.That(JournalContains(session, "Day-5 decision: the crew conserves its reserves"), Is.True);
            Assert.That(flow.TryChoose(ChapterChoice.ConserveReserves).Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "decided once");
            Assert.That(flow.CheckContinueRoutine().Succeeded, Is.True, flow.CheckContinueRoutine().Explanation);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Day5_WithNoSciencePayloadAboard_GrowOrConserveIsARealPaidDecision(bool build)
        {
            using (MissionSession session = Accident(presetId: AuroraMission.RedundancyPresetId))
            {
                Assert.That(session.Planner.LaunchedConfiguration.HasSciencePayload, Is.False, "the redundancy loadout carries no payload");
                CompleteAccidentBeat(session);
                RunRoutine(session);
                AssertGrowOrConserveOpen(session);
                Assert.That(session.ChapterFlow.CheckChoose(ChapterChoice.ConserveReserves).Explanation, Does.Contain("alternative"),
                    "the forecast names the affordable alternative");
                if (build)
                {
                    BuildThroughTheExistingTarget(session);
                }
                else
                {
                    ConserveReserves(session);

                    // Conserving locks nothing and refunds nothing: a later build still pays its full cost.
                    int parts = session.Resources.Snapshot.SparePartsCount;
                    Require(session.Survival.TryBuildGrowModule());
                    Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(parts - session.Survival.Spec.BuildSpareParts));
                    Assert.That(session.ChapterFlow.ConsequencesResolution, Is.EqualTo("Reserves conserved"), "the decision stays as taken");
                }

                RunRoutine(session);
                Assert.That(session.ChapterFlow.Chapter, Is.EqualTo(MissionChapter.Contact));
            }
        }

        [Test]
        public void Day5_AfterTheInstrumentWasAlreadySalvaged_GrowOrConserveOpens_WithoutASecondCredit()
        {
            using (MissionSession session = Accident())
            {
                CompleteAccidentBeat(session);
                IInteractable salvage = Interaction(session, InteractionIds.SalvagePayload);
                int credit = session.FieldModifications.SalvageablePartsNow;
                Assert.That(credit, Is.GreaterThan(0));
                int parts = session.Resources.Snapshot.SparePartsCount;
                Require(salvage.TryInteract());
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(parts + credit));
                Assert.That(session.ChapterFlow.PendingDecision, Is.EqualTo(ChapterDecision.None), "no day-5 decision before day 5");
                int afterSalvage = session.Resources.Snapshot.SparePartsCount;

                RunRoutine(session);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(afterSalvage), "nothing is credited again at the beat");
                Assert.That(salvage.TryInteract().Succeeded, Is.False, "the instrument is gone");
                AssertGrowOrConserveOpen(session);
                BuildThroughTheExistingTarget(session);
            }
        }

        [Test]
        public void Day5_WhenThisShipCannotReachTheInstrument_GrowOrConserveOpens_AndConservingSettlesIt()
        {
            using (MissionSession session = Accident(salvage: false))
            {
                Assert.That(session.Planner.LaunchedConfiguration.HasSciencePayload, Is.True);
                CompleteAccidentBeat(session);
                RunRoutine(session);
                Assert.That(session.FieldModifications.SalvageablePartsNow, Is.EqualTo(0));
                AssertGrowOrConserveOpen(session);
                ConserveReserves(session);
            }
        }

        [Test]
        public void Day5_AGrowModuleBuiltBeforeTheInspection_IsRecordedAsTheTakenDecision_NotSkippedSilently()
        {
            using (MissionSession session = Accident(presetId: AuroraMission.RedundancyPresetId))
            {
                CompleteAccidentBeat(session);
                Require(Interaction(session, InteractionIds.BuildGrowModule).TryInteract());
                int builtDay = MissionCalendar.DayNumberAt(session.Survival.GrowModuleBuiltAtSimulationSeconds.Value);
                Assert.That(builtDay, Is.LessThan(5));
                RunRoutine(session);
                MissionChapterFlow flow = session.ChapterFlow;
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Consequences));
                Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.None));
                Assert.That(flow.ConsequencesResolution, Is.EqualTo("Grow module built on mission day " + builtDay));
                Assert.That(JournalContains(session, "Day-5 decision already taken"), Is.True, "recorded, not silent");
                Assert.That(flow.TryChoose(ChapterChoice.ConserveReserves).Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(flow.CheckContinueRoutine().Succeeded, Is.True);
            }
        }

        [Test]
        public void ARoutineStopsAtTheRealTimeAFoodStoreRunsOut_AndTheFailedSurvivalPathEndsInALoss()
        {
            using (MissionSession session = Accident())
            {
                CompleteAccidentBeat(session);
                // Leave food for about one more day.
                double leave = session.Survival.RationFoodKg * 4d;
                Require(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.FoodKg, session.Resources.Snapshot.FoodKg - leave) }));

                RunRoutine(session);
                MissionChapterFlow flow = session.ChapterFlow;
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.ResourceShortage), flow.LastRoutineStopText);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Accident), "no beat is skipped past a failed boundary");
                double stoppedAt = flow.RoutineStoppedAtSimulationSeconds.Value;
                Assert.That(stoppedAt, Is.LessThan(MissionChapterFlow.BeatStart(MissionChapter.Consequences)));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(stoppedAt), "the frame ends at the stop: nothing runs on at normal speed");
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "the crew is alive when the routine stops");
                Assert.That(flow.TryContinueRoutine().Succeeded, Is.False, "no routine on an empty store");

                // Played on (drinking whenever water is due), the hunger limit is reached: the failed-survival path ends as Lost.
                for (int frame = 0; session.State.IsSimulationRunning; frame++)
                {
                    Assert.That(frame, Is.LessThan(20000));
                    if (session.Survival.CheckDrinkWater().Succeeded)
                    {
                        Require(session.Survival.TryDrinkWater());
                    }

                    Require(session.Tick(Frame));
                }

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.HungerLimit));
            }
        }

        [Test]
        public void PauseSuspendsARoutine_CancelKeepsTheCurrentTime_AndRestartLeavesNothingBehind()
        {
            MissionChapterFlow oldFlow;
            using (MissionSession session = Accident())
            {
                CompleteAccidentBeat(session);
                MissionChapterFlow flow = session.ChapterFlow;
                oldFlow = flow;
                Require(flow.TryContinueRoutine());
                Require(session.Tick(1d));
                double now = session.State.ElapsedSimulationSeconds;
                ResourceSnapshot reserves = session.Resources.Snapshot;
                Assert.That(session.Clock.Pause().Succeeded, Is.True);
                Require(session.Tick(5d));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(now), "paused: no time");
                Fixtures.AssertSnapshot(reserves, session.Resources.Snapshot, 0d);
                Assert.That(flow.IsRoutineRunning, Is.True, "pause suspends, it does not end the routine");
                Require(flow.TryCancelRoutine());
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.PlayerCancelled));
                Assert.That(flow.RoutineStoppedAtSimulationSeconds, Is.EqualTo(now));
                Assert.That(session.SurvivalRecovery.IsResting, Is.False);
                Assert.That(session.Clock.Resume().Succeeded, Is.True);
                Assert.That(session.Clock.TimeScale, Is.EqualTo(session.Definition.Scenario.Pacing.InteriorTimeScale));
                Assert.That(flow.TryCancelRoutine().Succeeded, Is.False);

                Require(flow.TryContinueRoutine());
            }

            Assert.That(oldFlow.IsRoutineRunning, Is.False, "a disposed session has no running routine");
            Assert.That(oldFlow.TryContinueRoutine().Succeeded, Is.False);
            using (MissionSession fresh = Accident())
            {
                Assert.That(fresh.ChapterFlow.Chapter, Is.EqualTo(MissionChapter.Accident));
                Assert.That(fresh.ChapterFlow.Revision, Is.LessThan(5));
            }
        }

        /// <summary>
        /// The complete observable state two runs must share: clock, every reserve (oxygen, water, food, battery, CO2,
        /// parts), the needs, rest and portion counters, the routine record and the chapter.
        /// </summary>
        private static void AssertSameState(MissionSession expected, MissionSession actual, string what)
        {
            Assert.That(actual.State.ElapsedSimulationSeconds, Is.EqualTo(expected.State.ElapsedSimulationSeconds).Within(1e-6), what + ": clock");
            Assert.That(actual.Clock.TimeScale, Is.EqualTo(expected.Clock.TimeScale), what + ": time scale");
            Assert.That(actual.Clock.IsPaused, Is.EqualTo(expected.Clock.IsPaused), what + ": pause");
            Assert.That(actual.State.Phase, Is.EqualTo(expected.State.Phase), what + ": phase");
            Assert.That(actual.State.Outcome, Is.EqualTo(expected.State.Outcome), what + ": outcome");
            ResourceSnapshot a = expected.Resources.Snapshot;
            ResourceSnapshot b = actual.Resources.Snapshot;
            Assert.That(b.OxygenKg, Is.EqualTo(a.OxygenKg).Within(1e-6), what + ": oxygen");
            Assert.That(b.WaterLitres, Is.EqualTo(a.WaterLitres).Within(1e-6), what + ": water");
            Assert.That(b.FoodKg, Is.EqualTo(a.FoodKg).Within(1e-6), what + ": food");
            Assert.That(b.BatteryKWh, Is.EqualTo(a.BatteryKWh).Within(1e-6), what + ": battery");
            Assert.That(b.CarbonDioxidePpm, Is.EqualTo(a.CarbonDioxidePpm).Within(1e-3), what + ": CO2");
            Assert.That(b.SparePartsCount, Is.EqualTo(a.SparePartsCount), what + ": parts");
            Assert.That(actual.Survival.Hunger01, Is.EqualTo(expected.Survival.Hunger01).Within(1e-9), what + ": hunger");
            Assert.That(actual.Survival.Thirst01, Is.EqualTo(expected.Survival.Thirst01).Within(1e-9), what + ": thirst");
            Assert.That(actual.Survival.RationsEaten, Is.EqualTo(expected.Survival.RationsEaten), what + ": rations");
            Assert.That(actual.Survival.DrinksTaken, Is.EqualTo(expected.Survival.DrinksTaken), what + ": drinks");
            Assert.That(actual.SurvivalRecovery.IsResting, Is.EqualTo(expected.SurvivalRecovery.IsResting), what + ": resting");
            Assert.That(actual.SurvivalRecovery.RestCount, Is.EqualTo(expected.SurvivalRecovery.RestCount), what + ": rests");
            Assert.That(actual.SurvivalRecovery.RestedSimulationSeconds, Is.EqualTo(expected.SurvivalRecovery.RestedSimulationSeconds).Within(1e-6), what + ": rested");
            Assert.That(actual.SurvivalRecovery.Stage, Is.EqualTo(expected.SurvivalRecovery.Stage), what + ": recovery stage");
            MissionChapterFlow x = expected.ChapterFlow;
            MissionChapterFlow y = actual.ChapterFlow;
            Assert.That(y.Chapter, Is.EqualTo(x.Chapter), what + ": chapter");
            Assert.That(y.IsRoutineRunning, Is.EqualTo(x.IsRoutineRunning), what + ": routine running");
            Assert.That(y.LastRoutineStop, Is.EqualTo(x.LastRoutineStop), what + ": stop reason");
            Assert.That(y.RoutineStoppedAtSimulationSeconds.Value, Is.EqualTo(x.RoutineStoppedAtSimulationSeconds.Value).Within(1e-6), what + ": stop time");
            Assert.That(new[] { y.RoutineRations, y.RoutineDrinks, y.RoutineRests }, Is.EqualTo(new[] { x.RoutineRations, x.RoutineDrinks, x.RoutineRests }), what + ": tally");
            Assert.That(y.PendingDecision, Is.EqualTo(x.PendingDecision), what + ": decision");
        }

        /// <summary>Starts the routine and drives it with one frame of <paramref name="realSeconds"/> (the frame is not clamped).</summary>
        private static void OneFrameRoutine(MissionSession session, double realSeconds)
        {
            Require(session.ChapterFlow.TryContinueRoutine());
            Require(session.Tick(realSeconds));
            Assert.That(session.ChapterFlow.IsRoutineRunning, Is.False, "one frame is enough");
        }

        [Test]
        public void OneHugeFrame_EndsAtTheRoutineStop_WithTheSameFullStateAsSmallFrames()
        {
            using (MissionSession small = Accident())
            using (MissionSession huge = Accident())
            {
                CompleteAccidentBeat(small);
                RunRoutine(small);
                CompleteAccidentBeat(huge);
                OneFrameRoutine(huge, 1000d);

                Assert.That(huge.ChapterFlow.LastRoutineStop, Is.EqualTo(RoutineStopReason.ReachedChapter));
                foreach (MissionSession session in new[] { small, huge })
                {
                    Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(session.ChapterFlow.RoutineStoppedAtSimulationSeconds.Value),
                        "the rest of the frame is discarded at the stop");
                }

                AssertSameState(small, huge, "small frames vs one 1000 s frame");
            }
        }

        [Test]
        public void AHugeFrameCannotSpendTheBeaconWindow_TheRequestStopsTheFrameWithTheWholeWindowOpen()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                SurvivalRecovery recovery = session.SurvivalRecovery;
                CompleteAccidentBeat(session);
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.PreserveScience));
                RunRoutine(session);
                ReachContact(session);
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.AcknowledgeRescueFailure));
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.KeepGrowRunning));
                double requestAt = recovery.ArrivalAtSimulationSeconds.Value - (MissionChapterFlow.BeaconRequestBeforeArrivalHours * Hour);
                while (flow.NextChapterAtSimulationSeconds.Value < requestAt - Tolerance)
                {
                    // CHAPTER-PACING-01: the 3-day steps up to the last one before the request.
                    RunSegment(session, true);
                    Assert.That(flow.LastStopWasStep, Is.True, flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                }

                OneFrameRoutine(session, 1000d);
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(requestAt).Within(Tolerance), "the frame ends at the request");
                Assert.That(recovery.BeaconWindowEndsAtSimulationSeconds.Value - session.State.ElapsedSimulationSeconds,
                    Is.EqualTo(SurvivalRecovery.BeaconWindowHours * Hour).Within(Tolerance), "the whole window is still open");
                Assert.That(recovery.BeaconMissed, Is.False);
                Require(recovery.TryPowerBeacon());
            }
        }

        [Test]
        public void AJumpDuringARoutine_EndsAtTheRoutineStop_NotAtTheEndOfTheJump()
        {
            using (MissionSession session = Accident())
            {
                CompleteAccidentBeat(session);
                MissionChapterFlow flow = session.ChapterFlow;
                Require(flow.TryContinueRoutine());
                double jumped = session.JumpedSimulationSeconds;
                double before = session.State.ElapsedSimulationSeconds;
                Require(session.AdvanceSimulation(20d * Day));
                Assert.That(flow.IsRoutineRunning, Is.False);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Consequences));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(flow.RoutineStoppedAtSimulationSeconds.Value), "the jump ends at the beat");
                Assert.That(session.JumpedSimulationSeconds - jumped, Is.EqualTo(session.State.ElapsedSimulationSeconds - before).Within(1e-6));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
            }
        }

        /// <summary>
        /// Day 5, decision taken, then life support fails completely (an explicit test fault through the public damage
        /// command): cabin CO2 rises while the crew is awake and the power is fine.
        /// </summary>
        private static MissionSession RisingCarbonDioxideAtDayFive()
        {
            MissionSession session = Accident();
            CompleteAccidentBeat(session);
            RunRoutine(session);
            Require(session.ChapterFlow.TryChoose(ChapterChoice.PreserveScience));
            Require(session.Systems.LifeSupport.TryDamage(0d));
            Require(session.AdvanceSimulation(1d));
            Assert.That(session.Resources.Rates.CarbonDioxideChangePpmPerSecond, Is.GreaterThan(0d), "CO2 rises");
            Assert.That(session.Director.IsBrownout, Is.False, "the power is fine");
            return session;
        }

        [Test]
        public void RisingCarbonDioxideWhileAwake_StopsTheRoutineAtTheDangerMargin_NeverAtTheLossLimit()
        {
            using (MissionSession small = RisingCarbonDioxideAtDayFive())
            using (MissionSession huge = RisingCarbonDioxideAtDayFive())
            {
                double untilLimit = small.Resources.TimeUntilCarbonDioxideReaches(SurvivalRules.CarbonDioxideLossLimitPpm);
                double margin = SurvivalRecovery.CarbonDioxideDangerMarginSeconds;
                double expectedStop = small.State.ElapsedSimulationSeconds + untilLimit - margin;
                Assert.That(untilLimit, Is.GreaterThan(margin + Hour), "the crew starts outside the danger margin");
                Assert.That(expectedStop, Is.LessThan(MissionChapterFlow.BeatStart(MissionChapter.Contact)), "the danger comes before the next beat");
                Assert.That(small.ChapterFlow.CheckContinueRoutine().Succeeded, Is.True, small.ChapterFlow.CheckContinueRoutine().Explanation);

                RunRoutine(small);
                OneFrameRoutine(huge, 1000d);
                foreach (MissionSession session in new[] { small, huge })
                {
                    MissionChapterFlow flow = session.ChapterFlow;
                    Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.Danger), flow.LastRoutineStopText);
                    Assert.That(flow.LastRoutineStopText, Does.Contain("CO2"));
                    Assert.That(flow.RoutineStoppedAtSimulationSeconds.Value, Is.EqualTo(expectedStop).Within(1e-3), "exactly at the margin");
                    Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(flow.RoutineStoppedAtSimulationSeconds.Value));
                    Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "the crew is alive and warned");
                    Assert.That(session.Resources.TimeUntilCarbonDioxideReaches(SurvivalRules.CarbonDioxideLossLimitPpm), Is.EqualTo(margin).Within(1e-3));
                    Assert.That(flow.TryContinueRoutine().Succeeded, Is.False, "no routine inside the danger margin");
                }

                AssertSameState(small, huge, "small frames vs one 1000 s frame at the CO2 warning");
            }
        }

        [Test]
        public void ARuntimeReleaseStopsTheRoutineAndWakesTheCrew()
        {
            using (MissionSession session = Accident())
            {
                CompleteAccidentBeat(session);
                MissionChapterFlow flow = session.ChapterFlow;
                Require(flow.TryContinueRoutine());
                for (int frame = 0; !session.SurvivalRecovery.IsResting; frame++)
                {
                    Assert.That(frame, Is.LessThan(2000));
                    Require(session.Tick(Frame));
                }

                session.SurvivalRecovery.ReleaseForDisabledRuntime();
                Assert.That(flow.IsRoutineRunning, Is.False);
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.PlayerCancelled));
                Assert.That(session.SurvivalRecovery.IsResting, Is.False);
                Assert.That(session.Clock.TimeScale, Is.EqualTo(session.Definition.Scenario.Pacing.InteriorTimeScale));
            }
        }

        [Test]
        public void Day20_StoppingTheGrowModuleForfeitsTheCrop_WithoutCreditOrRefund()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                CompleteAccidentBeat(session);
                RunRoutine(session);
                Assert.That(session.Interactions.TryGet(InteractionIds.SalvagePayload, out IInteractable salvage), Is.True);
                Require(salvage.TryInteract());
                RunRoutine(session);
                ReachContact(session);
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.AcknowledgeRescueFailure));
                // Late enough that the crop is still growing on day 20 (8 days to ripen).
                RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.FinalDecision), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                Require(session.Survival.TryBuildGrowModule());
                Require(session.Survival.TryPlant());
                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Growing));

                ResourceSnapshot before = session.Resources.Snapshot;
                Require(flow.CheckChoose(ChapterChoice.StopGrowForBeacon));
                Require(flow.TryChoose(ChapterChoice.StopGrowForBeacon));
                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Failed));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);
                Assert.That(session.Survival.TryHarvest().Succeeded, Is.False, "no harvest from a stopped module");
                Assert.That(flow.FinalDecision, Is.EqualTo(ChapterChoice.StopGrowForBeacon));
                Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.None));
            }
        }

        [Test]
        public void AMissedBeaconOpensOneSecondPaidWindow_NeverAnAutomaticRecovery()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                SurvivalRecovery recovery = session.SurvivalRecovery;
                CompleteAccidentBeat(session);
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.PreserveScience));
                RunRoutine(session);
                ReachContact(session);
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.AcknowledgeRescueFailure));
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.KeepGrowRunning));
                RunRoutine(session);
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested));

                double firstArrival = recovery.ArrivalAtSimulationSeconds.Value;
                double windowEnds = recovery.BeaconWindowEndsAtSimulationSeconds.Value;
                Require(session.AdvanceSimulation(windowEnds - session.State.ElapsedSimulationSeconds));
                Assert.That(recovery.BeaconMissed, Is.True);
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.Delayed));
                Assert.That(recovery.SecondBeaconWindowOffered, Is.True);
                Assert.That(recovery.ArrivalAtSimulationSeconds.Value, Is.EqualTo(firstArrival + (3d * Day)).Within(Tolerance));

                // The routine runs to the second request (a time-critical stop), never straight to a docking.
                RunRoutine(session);
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.DecisionRequired), flow.LastRoutineStopText);
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                double battery = session.Resources.Snapshot.BatteryKWh;
                Require(recovery.TryPowerBeacon());
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.LessThan(battery), "the second window is paid too");

                RunRoutine(session);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered));
                Assert.That(MissionCalendar.DayNumberAt(recovery.RecoveredAtSimulationSeconds.Value), Is.EqualTo(33));
            }
        }

        /// <summary>Reaches the rendezvous beat with the beacon request open.</summary>
        private static MissionSession ToBeaconRequest()
        {
            MissionSession session = Accident();
            MissionChapterFlow flow = session.ChapterFlow;
            CompleteAccidentBeat(session);
            RunRoutine(session);
            Require(flow.TryChoose(ChapterChoice.PreserveScience));
            RunRoutine(session);
            ReachContact(session);
            RunRoutine(session);
            Require(flow.TryChoose(ChapterChoice.AcknowledgeRescueFailure));
            RunRoutine(session);
            Require(flow.TryChoose(ChapterChoice.KeepGrowRunning));
            RunRoutine(session);
            Assert.That(session.SurvivalRecovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested));
            return session;
        }

        [Test]
        public void TwoMissedBeaconWindows_EndInADocumentedMissedRendezvous_NoDocking()
        {
            using (MissionSession session = ToBeaconRequest())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                Require(session.AdvanceSimulation(recovery.BeaconWindowEndsAtSimulationSeconds.Value - session.State.ElapsedSimulationSeconds));
                RunRoutine(session);
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested), "second request");
                Require(session.AdvanceSimulation(recovery.BeaconWindowEndsAtSimulationSeconds.Value - session.State.ElapsedSimulationSeconds));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.State.OutcomeReason, Does.Contain("Missed rendezvous"));
                Assert.That(recovery.RecoveredAtSimulationSeconds, Is.Null);
            }
        }

        [Test]
        public void EarlyExteriorWorkAndContactOnDay1_AreKept_AndTheNewsStillComesCausallyOnDay14()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                ReachContact(session);
                Assert.That(MissionCalendar.DayNumberAt(session.State.ElapsedSimulationSeconds), Is.EqualTo(1));
                Assert.That(flow.Rescue, Is.EqualTo(RescuePlan.FirstRescuePlanned));
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Accident), "the beats keep their order");
                Require(session.Survival.TryConsumeRation());
                Require(session.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(session.AdvanceSimulation(SurvivalRecovery.MaximumRestHours * Hour));
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.PreserveScience));
                Assert.That(flow.NextChapter, Is.EqualTo(MissionChapter.RescueFailed), "contact is done: the day-9 stop has nothing left");
                Assert.That(flow.LatestDispatch.Id, Is.Null, "no news before it can arrive");
                RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.RescueFailed), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                AssertCausalDispatch(session, flow.LatestDispatch);
                Assert.That(MissionCalendar.DayNumberAt(flow.LatestDispatch.ReceivedAtSimulationSeconds), Is.EqualTo(14));
            }
        }

        [Test]
        public void ALateContactPostponesTheNews_ItIsNeverRetroactive()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                CompleteAccidentBeat(session);
                RunRoutine(session);
                Require(flow.TryChoose(ChapterChoice.PreserveScience));
                RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Contact));

                // The crew waits until day 15 before the exterior work, eating and drinking by hand meanwhile.
                double day15 = (14d * Day) + (10d * Hour);
                while (session.State.ElapsedSimulationSeconds < day15)
                {
                    Assert.That(session.State.IsSimulationRunning, Is.True);
                    if (session.Survival.CheckDrinkWater().Succeeded)
                    {
                        Require(session.Survival.TryDrinkWater());
                    }

                    if (session.Survival.CheckConsumeRation().Succeeded)
                    {
                        Require(session.Survival.TryConsumeRation());
                    }

                    Require(session.AdvanceSimulation(Math.Min(Hour, day15 - session.State.ElapsedSimulationSeconds)));
                }

                ReachContact(session);
                double contactAt = session.State.ElapsedSimulationSeconds;
                Assert.That(flow.LatestDispatch.Id, Is.Null);
                RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.RescueFailed), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                AssertCausalDispatch(session, flow.LatestDispatch);
                Assert.That(flow.LatestDispatch.TransmittedAtSimulationSeconds, Is.GreaterThanOrEqualTo(contactAt));
                Assert.That(flow.LatestDispatch.ReceivedAtSimulationSeconds, Is.GreaterThan(contactAt));
            }
        }

        [Test]
        public void ARoutineRunningWhileEarthsReplyTravels_StopsAtTheActualReply_AtTheSurvivalSpeed()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                CompleteAccidentBeat(session);
                Fixtures.CompleteEva(session);
                Fixtures.Transmit(session);
                double replyDue = session.Communications.ReplyDueAtSimulationSeconds.Value;
                Segment segment = RunRoutine(session);
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.DispatchReceived), flow.LastRoutineStopText);
                Assert.That(flow.RoutineStoppedAtSimulationSeconds.Value, Is.EqualTo(replyDue).Within(Tolerance));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
                Assert.That(flow.Rescue, Is.EqualTo(RescuePlan.FirstRescuePlanned));
                Assert.That(session.Clock.TimeScale, Is.EqualTo(session.Survival.Spec.TimeScale));
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Accident), "no beat skipped");
                Assert.That(segment.Frames, Is.GreaterThan(0));
            }
        }

        /// <summary>Repairs and the first paid meal and water of day 1, without any rest.</summary>
        private static void RepairsAndFirstMeal(MissionSession session)
        {
            Fixtures.RepairOrFail(session, ShipSystemId.Power);
            Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
            double due = session.Survival.Spec.PortionSeconds - (session.State.ElapsedSimulationSeconds - session.Survival.ActivatedAtSimulationSeconds.Value);
            if (due > 0d)
            {
                Require(session.AdvanceSimulation(due));
            }

            Require(session.Survival.TryConsumeRation());
            Require(session.Survival.TryDrinkWater());
        }

        private static IInteractable Berth(MissionSession session)
        {
            Assert.That(session.Interactions.TryGet(InteractionIds.SurvivalSleep, out IInteractable berth), Is.True);
            return berth;
        }

        /// <summary>The berth after a stop: waits out the morning hour if the crew has just woken, then sleeps.</summary>
        private static Segment StorySleep(MissionSession session, IInteractable berth)
        {
            if (!berth.CanInteract && berth.Prompt.Contains("morning meal and checks first"))
            {
                double woke = session.SurvivalRecovery.LastRestEndedAtSimulationSeconds.Value;
                Require(session.AdvanceSimulation((woke + (MissionChapterFlow.MinimumAwakeHoursBeforeNight * Hour)) - session.State.ElapsedSimulationSeconds + 1d));
            }

            Assert.That(berth.CanInteract, Is.True, berth.Prompt);
            Assert.That(berth.Prompt, Does.StartWith("Sleep and continue to"));
            ResourceSnapshot before = session.Resources.Snapshot;
            double restedBefore = session.SurvivalRecovery.RestedSimulationSeconds;
            double at = session.State.ElapsedSimulationSeconds;
            Require(berth.TryInteract());
            MissionChapterFlow flow = session.ChapterFlow;
            Assert.That(flow.IsRoutineRunning, Is.True, "the berth started the routine");
            Assert.That(session.SurvivalRecovery.IsResting, Is.True, "the story sleep's night begins at once");

            // Spam: every further press and R during the routine is refused and changes nothing.
            int revision = flow.Revision;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(berth.TryInteract().Succeeded, Is.False, "no second start while the routine runs");
                Assert.That(flow.TryContinueRoutine().Succeeded, Is.False);
            }

            Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(at), "refused presses advance nothing");
            Assert.That(flow.Revision, Is.EqualTo(revision), "refused presses commit nothing");
            Segment segment = RunSegment(session, false);
            segment.Before = before;
            segment.RestedBefore = restedBefore;
            segment.StartedAt = at;
            return segment;
        }

        [Test]
        public void TheBerthIsTheStorySleep_Day1To5_ThenTo14_ThenStepsOfAtMostThreeDaysTo30_WithoutADoubleSkip()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                SurvivalRecovery recovery = session.SurvivalRecovery;
                IInteractable berth = Berth(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                ReachContact(session);
                Assert.That(MissionCalendar.DayNumberAt(session.State.ElapsedSimulationSeconds), Is.EqualTo(1), "contact made early");
                Require(session.Survival.TryConsumeRation());
                var stops = new List<int>();
                var chapters = new List<MissionChapter>();

                // First story sleep: day 1 -> day 5.
                Segment first = StorySleep(session, berth);
                AssertFoodReconciles(session, first);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Consequences), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                Assert.That(MissionCalendar.DayNumberAt(session.State.ElapsedSimulationSeconds), Is.EqualTo(5));
                Assert.That(berth.CanInteract, Is.False, "the day-5 decision comes first");
                Require(flow.TryChoose(ChapterChoice.PreserveScience));

                // Second story sleep: day 5 -> the day-14 news (no day-9 stop: contact was made).
                Segment second = StorySleep(session, berth);
                AssertFoodReconciles(session, second);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.RescueFailed), flow.LastRoutineStop + ": " + flow.LastRoutineStopText);
                Assert.That(MissionCalendar.DayNumberAt(session.State.ElapsedSimulationSeconds), Is.EqualTo(14));
                AssertCausalDispatch(session, flow.LatestDispatch);
                Require(flow.TryChoose(ChapterChoice.AcknowledgeRescueFailure));

                // After the news: steps of at most three days up to the rendezvous on day 30.
                int transitions = 0;
                MissionChapter chapter = flow.Chapter;
                while (recovery.Stage != RecoveryStage.BeaconRequested)
                {
                    Assert.That(stops.Count, Is.LessThan(10), "the steps reach day 30");
                    if (flow.PendingDecision == ChapterDecision.GrowOrBeacon)
                    {
                        Require(flow.TryChoose(ChapterChoice.KeepGrowRunning));
                    }

                    Segment step = StorySleep(session, berth);
                    AssertFoodReconciles(session, step);
                    Assert.That(flow.RoutineStoppedAtSimulationSeconds.Value - step.StartedAt, Is.LessThanOrEqualTo((3d * Day) + Tolerance), "at most 3 days per step");
                    stops.Add(MissionCalendar.DayNumberAt(session.State.ElapsedSimulationSeconds));
                    if (flow.Chapter != chapter)
                    {
                        transitions++;
                        chapters.Add(flow.Chapter);
                        chapter = flow.Chapter;
                    }
                }

                Assert.That(stops, Is.EqualTo(new[] { 17, 20, 23, 26, 29, 30 }), "Alex's pacing after the news");
                Assert.That(chapters, Is.EqualTo(new[] { MissionChapter.FinalDecision, MissionChapter.Rendezvous }), "each beat is entered exactly once");
                Assert.That(transitions, Is.EqualTo(2));
                Require(recovery.TryPowerBeacon());
                Segment docking = RunSegment(session, true);
                AssertFoodReconciles(session, docking);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered));
                Assert.That(MissionCalendar.DayNumberAt(recovery.RecoveredAtSimulationSeconds.Value), Is.EqualTo(30));
            }
        }

        [Test]
        public void WithoutContact_TheSecondStepStopsAtDay9_WhereTheExteriorWorkIsStillDue()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                IInteractable berth = Berth(session);
                RepairsAndFirstMeal(session);
                StorySleep(session, berth);
                Require(flow.TryChoose(ChapterChoice.PreserveScience));
                Assert.That(flow.NextChapter, Is.EqualTo(MissionChapter.Contact), "no contact yet: the day-9 stop stays");
                StorySleep(session, berth);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Contact));
                Assert.That(MissionCalendar.DayNumberAt(session.State.ElapsedSimulationSeconds), Is.EqualTo(9));
                Assert.That(flow.CheckContinueRoutine().Explanation, Does.Contain("antenna"));
            }
        }

        [Test]
        public void AnOrdinaryRestNeedsAFullWakingDay_TheBerthSaysWhy_AndAWakeCannotChainNights()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                SurvivalRecovery recovery = session.SurvivalRecovery;
                IInteractable berth = Berth(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);

                // No story sleep before the first meal, and the crew is not tired yet: refused with the reason, nothing changes.
                Assert.That(berth.CanInteract, Is.False);
                Assert.That(berth.Prompt, Does.Contain("Not tired yet").And.Contain("Prepare and eat the first stored ration first."));
                double before = session.State.ElapsedSimulationSeconds;
                Assert.That(berth.TryInteract().Succeeded, Is.False);
                Assert.That(recovery.IsResting, Is.False);
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(before));

                // After a full ordinary waking day an ordinary rest is allowed (it is not a routine).
                double awakeSince = session.Survival.ActivatedAtSimulationSeconds.Value;
                Require(session.AdvanceSimulation((awakeSince + (MissionChapterFlow.RoutineAwakeHours * 3600d)) - session.State.ElapsedSimulationSeconds + 1d));
                Assert.That(berth.CanInteract, Is.True, berth.Prompt);
                Require(berth.TryInteract());
                Assert.That(recovery.IsResting, Is.True);
                Assert.That(flow.IsRoutineRunning, Is.False, "an ordinary rest, not a routine");
                Require(berth.TryInteract());
                Assert.That(recovery.IsResting, Is.False, "the berth wakes the crew");

                // Right after the wake neither an ordinary rest nor (after the meal) a story sleep can start.
                Assert.That(berth.CanInteract, Is.False, berth.Prompt);
                Require(session.Survival.TryConsumeRation());
                Assert.That(berth.CanInteract, Is.False, "morning hour after the wake");
                Assert.That(berth.Prompt, Does.Contain("morning meal and checks first"));
                Require(session.AdvanceSimulation((MissionChapterFlow.MinimumAwakeHoursBeforeNight * 3600d) + 1d));
                Assert.That(berth.CanInteract, Is.True, berth.Prompt);
                Assert.That(berth.Prompt, Does.StartWith("Sleep and continue to the inspection of the repairs and reserves, mission day 5"));
            }
        }
    }
}
