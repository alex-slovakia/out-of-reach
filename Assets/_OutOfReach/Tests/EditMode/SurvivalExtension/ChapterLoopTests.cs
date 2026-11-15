using System;
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
        private static MissionSession Accident(bool salvage = true)
        {
            var session = new MissionSession(survivalExtension: true, chapterLoop: true);
            session.EnableFieldModifications(salvage, false);
            Require(session.Planner.TryLaunch());
            Fixtures.TriggerImpact(session);
            return session;
        }

        /// <summary>Repairs, the first meal and water, one complete rest: the day 1-2 beat is done.</summary>
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
        }

        /// <summary>
        /// Starts a routine and drives real frames until it stops. Every frame: no store may grow, no rest may exceed
        /// 8.5 h, player control is off, and the hunger/thirst stay below their limits unless a store is empty.
        /// </summary>
        private static Segment RunRoutine(MissionSession session)
        {
            MissionChapterFlow flow = session.ChapterFlow;
            var segment = new Segment
            {
                Before = session.Resources.Snapshot,
                StartedAt = session.State.ElapsedSimulationSeconds,
                RestedBefore = session.SurvivalRecovery.RestedSimulationSeconds,
            };
            Require(flow.TryContinueRoutine());
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
            Assert.That(session.Clock.TimeScale, Is.Not.EqualTo(MissionChapterFlow.RoutineTimeScale), "the routine scale ends with the routine");
            return segment;
        }

        /// <summary>Food: every ration Core issued plus the BVAD draw for the time actually rested, and nothing else.</summary>
        private static void AssertFoodReconciles(MissionSession session, Segment segment)
        {
            double rested = segment.RestedAfter - segment.RestedBefore;
            double expected = (session.ChapterFlow.RoutineRations * session.Survival.RationFoodKg)
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
        public void Day1_RoutineIsRefusedUntilRepairsMealAndOneCompleteRest_AndEvaWaitsForDay9()
        {
            using (MissionSession session = Accident())
            {
                MissionChapterFlow flow = session.ChapterFlow;
                Assert.That(flow.CheckContinueRoutine().Explanation, Does.Contain("power and life support"));
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Assert.That(flow.CheckContinueRoutine().Explanation, Does.Contain("ration"));
                Require(session.Survival.TryConsumeRation());
                Assert.That(flow.CheckContinueRoutine().Explanation, Does.Contain("rest"));
                Assert.That(session.Eva.CheckPrepareSuit().Succeeded, Is.True, "no date-only EVA gate: early exterior work is allowed");

                double before = session.State.ElapsedSimulationSeconds;
                Assert.That(flow.TryContinueRoutine().Succeeded, Is.False);
                Assert.That(flow.IsRoutineRunning, Is.False);
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(before), "a refusal changes nothing");
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
                Segment toDay5 = RunRoutine(session);
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

        [Test]
        public void Day5_WithoutASalvageableInstrument_NoDecisionBlocksTheRoutine()
        {
            using (MissionSession session = Accident(salvage: false))
            {
                CompleteAccidentBeat(session);
                RunRoutine(session);
                Assert.That(session.ChapterFlow.Chapter, Is.EqualTo(MissionChapter.Consequences), session.ChapterFlow.LastRoutineStop + ": " + session.ChapterFlow.LastRoutineStopText);
                Assert.That(session.ChapterFlow.PendingDecision, Is.EqualTo(ChapterDecision.None));
                Assert.That(session.ChapterFlow.CheckContinueRoutine().Succeeded, Is.True);
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
                Assert.That(session.State.ElapsedSimulationSeconds - stoppedAt, Is.InRange(0d, Frame * session.Definition.Scenario.Pacing.InteriorTimeScale),
                    "only the rest of the frame at the chapter's own speed follows the stop");
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

        [Test]
        public void OneHugeFrame_StopsAtTheSameTimeAndLedgerAsSmallFrames()
        {
            ResourceSnapshot small;
            double smallStop;
            int[] smallTally;
            using (MissionSession session = Accident())
            {
                CompleteAccidentBeat(session);
                RunRoutine(session);
                small = session.Resources.Snapshot;
                smallStop = session.ChapterFlow.RoutineStoppedAtSimulationSeconds.Value;
                smallTally = new[] { session.ChapterFlow.RoutineRations, session.ChapterFlow.RoutineDrinks, session.ChapterFlow.RoutineRests };
            }

            using (MissionSession session = Accident())
            {
                CompleteAccidentBeat(session);
                Require(session.ChapterFlow.TryContinueRoutine());
                for (int frame = 0; session.ChapterFlow.IsRoutineRunning; frame++)
                {
                    Assert.That(frame, Is.LessThan(200));
                    Require(session.Tick(30d));
                }

                MissionChapterFlow flow = session.ChapterFlow;
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.ReachedChapter));
                Assert.That(flow.RoutineStoppedAtSimulationSeconds.Value, Is.EqualTo(smallStop).Within(Tolerance), "same stop time");
                Assert.That(new[] { flow.RoutineRations, flow.RoutineDrinks, flow.RoutineRests }, Is.EqualTo(smallTally), "same commands");
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(small.FoodKg).Within(1e-6), "same food: awake food moves only by commands");
                Assert.That(session.Resources.Snapshot.WaterLitres, Is.EqualTo(small.WaterLitres).Within(1e-6), "same water");
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
                RunRoutine(session);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Contact));
                Assert.That(flow.CheckContinueRoutine().Succeeded, Is.True, "contact is already done: nothing is replayed");
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
    }
}
