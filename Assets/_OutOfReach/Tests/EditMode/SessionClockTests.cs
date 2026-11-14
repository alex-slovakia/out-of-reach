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
        public void ChapterJump_MatchesManySmallSteps_AcrossABatteryDepletion()
        {
            using (var jump = Fixtures.LaunchedSession(AuroraMission.ScienceFocusPresetId))
            using (var frames = Fixtures.LaunchedSession(AuroraMission.ScienceFocusPresetId))
            {
                // Emergency-like load: 10 kW net drain empties the 28 kWh battery after 10,080 s.
                var drain = new ResourceRates(OxygenKgPerSecond, 0d, 0d, 10000d, 0.05d);
                Assert.That(jump.Resources.TrySetRates(drain).Succeeded, Is.True);
                Assert.That(frames.Resources.TrySetRates(drain).Succeeded, Is.True);

                Assert.That(jump.AdvanceSimulation(4d * 3600d).Succeeded, Is.True);
                for (int i = 0; i < 1440; i++)
                {
                    frames.AdvanceSimulation(10d);
                }

                Fixtures.AssertSnapshot(jump.Resources.Snapshot, frames.Resources.Snapshot);
                Assert.That(jump.Resources.Snapshot.BatteryKWh, Is.EqualTo(0d));
                Assert.That(jump.Resources.GetDepletionTime(ResourceId.BatteryKWh), Is.EqualTo(10080d).Within(1e-6));
                Assert.That(frames.Resources.GetDepletionTime(ResourceId.BatteryKWh), Is.EqualTo(10080d).Within(1e-6));
                Assert.That(jump.State.ElapsedSimulationSeconds, Is.EqualTo(14400d).Within(Fixtures.Tolerance));
                Assert.That(frames.State.ElapsedSimulationSeconds, Is.EqualTo(14400d).Within(1e-6));
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
