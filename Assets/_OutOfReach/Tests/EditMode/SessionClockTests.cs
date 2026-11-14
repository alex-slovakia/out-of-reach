using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    public sealed class SessionClockTests
    {
        private const double OxygenKgPerSecond = CrewMetabolicAssumptions.OxygenKgPerCrewDay / CrewMetabolicAssumptions.SecondsPerDay;

        [Test]
        public void Planning_DoesNotAdvanceSimulationTime()
        {
            using (var session = Fixtures.NewSession())
            {
                CommandResult skip = session.AdvanceSimulation(60d);
                CommandResult tick = session.Tick(10d);

                Assert.That(skip.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(tick.Succeeded, Is.True);
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(0d));
                Assert.That(session.Resources.IsInitialized, Is.False);
            }
        }

        [Test]
        public void Tick_AppliesTimeScale_AndAdvancesEveryServiceOnce()
        {
            using (var session = Fixtures.LaunchedSession())
            {
                ResourceSnapshot start = session.Resources.Snapshot;
                Assert.That(session.Clock.TrySetTimeScale(60d).Succeeded, Is.True);

                session.Tick(1d);

                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(60d).Within(Fixtures.Tolerance));
                Assert.That(session.Resources.ElapsedSimulationSeconds, Is.EqualTo(60d).Within(Fixtures.Tolerance));
                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(start.OxygenKg - (OxygenKgPerSecond * 60d)).Within(1e-12));
            }
        }

        [Test]
        public void Pause_DiscardsRealTime_SoResumeHasNoCatchUpDrain()
        {
            using (var session = Fixtures.LaunchedSession())
            {
                ResourceSnapshot start = session.Resources.Snapshot;
                Assert.That(session.Clock.TrySetTimeScale(60d).Succeeded, Is.True);
                Assert.That(session.Clock.Pause().Succeeded, Is.True);
                Assert.That(session.Clock.Pause().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));

                session.Tick(1000d);

                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(0d));
                Fixtures.AssertSnapshot(start, session.Resources.Snapshot, 0d);

                Assert.That(session.Clock.Resume().Succeeded, Is.True);
                session.Tick(1d);

                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(60d).Within(Fixtures.Tolerance));
            }
        }

        [Test]
        public void Pause_BlocksBothTimeEntryPoints_TickAndChapterJump()
        {
            using (var session = Fixtures.EmergencySession())
            {
                ResourceSnapshot start = session.Resources.Snapshot;
                double elapsed = session.State.ElapsedSimulationSeconds;
                Assert.That(session.Clock.Pause().Succeeded, Is.True);

                CommandResult tick = session.Tick(60d);
                CommandResult jump = session.AdvanceSimulation(60d);

                Assert.That(tick.Succeeded, Is.True, "a paused frame is a successful no-op");
                Assert.That(jump.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "an explicit time jump is refused while paused");
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(elapsed));
                Assert.That(session.Resources.ElapsedSimulationSeconds, Is.EqualTo(elapsed));
                Fixtures.AssertSnapshot(start, session.Resources.Snapshot, 0d);

                Assert.That(session.Clock.Resume().Succeeded, Is.True);
                Assert.That(session.AdvanceSimulation(60d).Succeeded, Is.True);
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(elapsed + 60d).Within(Fixtures.Tolerance));
            }
        }

        [Test]
        public void TimeThatWouldOverflow_IsRejectedBeforeAnyMutation()
        {
            using (var session = Fixtures.LaunchedSession())
            {
                Assert.That(session.AdvanceSimulation(1e308).Succeeded, Is.True, "a huge but representable jump is still handled (the mission ends inside it)");
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(double.IsInfinity(session.State.ElapsedSimulationSeconds), Is.False);
            }

            using (var session = Fixtures.LaunchedSession())
            {
                session.State.AdvanceTime(1e308);
                ResourceSnapshot before = session.Resources.Snapshot;

                Assert.That(session.AdvanceSimulation(1e308).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.Throws<System.ArgumentOutOfRangeException>(() => session.State.AdvanceTime(1e308));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(1e308));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);
            }
        }

        [Test]
        public void InvalidTimeInputs_AreRejected_WithoutMutation([Values(-1d, double.NaN, double.PositiveInfinity)] double invalid)
        {
            using (var session = Fixtures.LaunchedSession())
            {
                ResourceSnapshot start = session.Resources.Snapshot;

                Assert.That(session.Tick(invalid).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.AdvanceSimulation(invalid).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Clock.TrySetTimeScale(invalid).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Clock.TrySetTimeScale(0d).Failure, Is.EqualTo(CommandFailure.InvalidInput));

                Assert.That(session.Clock.TimeScale, Is.EqualTo(1d));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(0d));
                Fixtures.AssertSnapshot(start, session.Resources.Snapshot, 0d);
            }
        }

        [Test]
        public void ChapterJump_MatchesManySmallSteps_WhenTheBatteryRunsFlatInsideTheJump()
        {
            using (var jump = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            using (var frames = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                // Life support repaired, power not: 1,400 W load against 380 W generation drains 27 kWh in
                // 27 / 1.02 h. From that exact moment the scrubbers stop and cabin CO2 starts to rise.
                Fixtures.RepairOrFail(jump, ShipSystemId.LifeSupport);
                Fixtures.RepairOrFail(frames, ShipSystemId.LifeSupport);
                double flatAt = 27d / 1.02d * 3600d;
                double span = 40d * 3600d;

                Assert.That(jump.AdvanceSimulation(span).Succeeded, Is.True);
                for (int i = 0; i < 240; i++)
                {
                    frames.AdvanceSimulation(600d);
                }

                double baseline = AuroraMission.InitialCabinCarbonDioxidePpm;
                double expectedCarbonDioxide = baseline + ((span - flatAt) * CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(1));
                Assert.That(expectedCarbonDioxide, Is.LessThan(SurvivalRules.CarbonDioxideLossLimitPpm), "test window stays below the loss limit");
                Assert.That(jump.Resources.GetDepletionTime(ResourceId.BatteryKWh), Is.EqualTo(flatAt).Within(1e-6));
                Assert.That(jump.Director.IsBrownout, Is.True);
                Assert.That(jump.Resources.Snapshot.CarbonDioxidePpm, Is.EqualTo(expectedCarbonDioxide).Within(1e-6),
                    "a single jump must stop scrubbing at the moment the battery empties, not at the end of the jump");
                Fixtures.AssertSnapshot(jump.Resources.Snapshot, frames.Resources.Snapshot, 1e-6);
                Assert.That(frames.Resources.GetDepletionTime(ResourceId.BatteryKWh), Is.EqualTo(flatAt).Within(1e-6));
                Assert.That(jump.State.ElapsedSimulationSeconds, Is.EqualTo(span).Within(1e-6));
                Assert.That(frames.State.ElapsedSimulationSeconds, Is.EqualTo(span).Within(1e-6));
            }
        }

        [Test]
        public void TimeScale_IsCapped_AndOversizedTicksChangeNothing()
        {
            using (var session = Fixtures.LaunchedSession())
            using (new EventRecorder(session.Events))
            {
                Assert.That(session.Clock.TrySetTimeScale(SimulationClock.MaximumTimeScale + 1d).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Clock.TrySetTimeScale(SimulationClock.MaximumTimeScale).Succeeded, Is.True);
                Assert.That(session.Director.TryCompleteCockpitCheck().Succeeded, Is.True);
                Assert.That(session.Cinematics.IsWaiting, Is.True);

                CommandResult oversized = session.Tick(double.MaxValue);

                Assert.That(oversized.Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Cinematics.IsWaiting, Is.True, "the rejected tick must not time the cinematic out");
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(0d));
            }
        }

        [Test]
        public void Pacing_SetsTheDisclosedTimeScalePerPhase()
        {
            using (var session = Fixtures.LaunchedSession())
            {
                MissionPacing pacing = session.Definition.Scenario.Pacing;
                Assert.That(session.Clock.TimeScale, Is.EqualTo(pacing.IntroTimeScale));

                Fixtures.TriggerImpact(session);
                Assert.That(session.Clock.TimeScale, Is.EqualTo(pacing.InteriorTimeScale));
                Assert.That(pacing.InteriorTimeScale, Is.EqualTo(300d), "documented default: one real second is five simulated minutes");

                Assert.That(session.Director.TryEndMission(MissionOutcome.Lost, "test").Succeeded, Is.True);
                Assert.That(session.Clock.TimeScale, Is.EqualTo(1d));
            }
        }

        [Test]
        public void PlayerControl_IsAllowedOnlyInRunningUnpausedPhasesWithoutACinematic()
        {
            using (var session = Fixtures.NewSession())
            using (new EventRecorder(session.Events))
            {
                Assert.That(session.IsPlayerControlAllowed, Is.False, "planning has no walking player");

                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                Assert.That(session.IsPlayerControlAllowed, Is.True);

                Assert.That(session.Clock.Pause().Succeeded, Is.True);
                Assert.That(session.IsPlayerControlAllowed, Is.False);
                Assert.That(session.Clock.Resume().Succeeded, Is.True);

                Assert.That(session.Director.TryCompleteCockpitCheck().Succeeded, Is.True);
                Assert.That(session.IsPlayerControlAllowed, Is.False, "input is locked while the impact plays");

                session.Tick(session.Definition.Scenario.ImpactCinematicTimeoutRealSeconds);
                Assert.That(session.IsPlayerControlAllowed, Is.True, "the fallback timeout always returns control");
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));

                Assert.That(session.Director.TryEndMission(MissionOutcome.Lost, "test").Succeeded, Is.True);
                Assert.That(session.IsPlayerControlAllowed, Is.False);
            }
        }

        [Test]
        public void SimulationStops_OnceTheMissionHasEnded()
        {
            using (var session = Fixtures.LaunchedSession())
            {
                session.AdvanceSimulation(100d);
                Assert.That(session.State.TryEndMission(MissionOutcome.Lost, "test", session.Resources.Snapshot).Succeeded, Is.True);
                ResourceSnapshot atEnd = session.Resources.Snapshot;

                CommandResult after = session.AdvanceSimulation(100d);
                session.Tick(100d);

                Assert.That(after.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(100d).Within(Fixtures.Tolerance));
                Fixtures.AssertSnapshot(atEnd, session.Resources.Snapshot, 0d);
            }
        }

        [Test]
        public void Restart_BuildsACleanMission_AndTheOldSessionKeepsNoListeners()
        {
            var first = Fixtures.LaunchedSession(AuroraMission.RedundancyPresetId);
            var oldRecorder = new EventRecorder(first.Events);
            Assert.That(first.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.SparePartsCount, 4d) }).Succeeded, Is.True);
            first.AdvanceSimulation(3600d);
            Assert.That(first.State.TryEndMission(MissionOutcome.Lost, "test", first.Resources.Snapshot).Succeeded, Is.True);
            int oldEvents = oldRecorder.Total;

            first.Dispose();

            Assert.That(first.Events.TotalSubscriberCount, Is.EqualTo(0));
            Assert.That(first.Tick(1d).Failure, Is.EqualTo(CommandFailure.WrongPhase));
            Assert.That(first.State.TryTransition(GamePhase.Report).Failure, Is.EqualTo(CommandFailure.WrongPhase), "a disposed session rejects commands");
            Assert.That(first.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.SparePartsCount, 1d) }).Failure, Is.EqualTo(CommandFailure.WrongPhase));
            Assert.That(first.Director.TryCompleteCockpitCheck().Failure, Is.EqualTo(CommandFailure.WrongPhase));

            using (var stalePlanning = Fixtures.NewSession())
            {
                stalePlanning.Dispose();
                Assert.That(stalePlanning.Planner.TryLaunch().Failure, Is.EqualTo(CommandFailure.WrongPhase), "a stale planner view cannot launch a disposed session");
                Assert.That(stalePlanning.Planner.TrySetEquipment(AuroraMission.OxygenTank, 1).Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(stalePlanning.State.Phase, Is.EqualTo(GamePhase.Planning));
            }

            using (var second = Fixtures.NewSession())
            {
                Assert.That(second.State.Phase, Is.EqualTo(GamePhase.Planning));
                Assert.That(second.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(second.State.ElapsedSimulationSeconds, Is.EqualTo(0d));
                Assert.That(second.Resources.IsInitialized, Is.False);
                Assert.That(second.Planner.HasLaunched, Is.False);
                Assert.That(second.Planner.DraftConfiguration.GetQuantity(AuroraMission.Spectrometer), Is.EqualTo(1), "default loadout, not the previous one");
                Assert.That(second.Planner.TryLaunch().Succeeded, Is.True);
                Assert.That(second.Resources.Snapshot.SparePartsCount, Is.EqualTo(7), "nothing spent in the old mission leaks");
                Assert.That(oldRecorder.Total, Is.EqualTo(oldEvents), "old listeners never hear the new mission");
            }
        }
    }
}
