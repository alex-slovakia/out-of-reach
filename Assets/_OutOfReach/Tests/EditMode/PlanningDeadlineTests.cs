using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;

namespace OutOfReach.Tests.EditMode
{
    public sealed class PlanningDeadlineTests
    {
        [Test]
        public void PracticePlanning_HasNoDeadline()
        {
            using (var session = Fixtures.NewSession())
            {
                session.Tick(100000d);

                Assert.That(session.PlanningDeadline.IsEnabled, Is.False);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
                Assert.That(session.PlanningDeadline.LastValidConfiguration, Is.Not.Null, "the valid starting loadout is remembered");
            }
        }

        [Test]
        public void Deadline_CountsUnpausedRealSeconds_AndLaunchesTheValidDraftOnce()
        {
            using (var session = Fixtures.NewSession(AuroraMission.RedundancyPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.PlanningDeadline.TryEnable().Succeeded, Is.True);
                Assert.That(session.PlanningDeadline.RemainingRealSeconds, Is.EqualTo(60d));

                session.Tick(20d);
                Assert.That(session.Clock.Pause().Succeeded, Is.True);
                session.Tick(500d);
                Assert.That(session.Clock.Resume().Succeeded, Is.True);
                Assert.That(session.PlanningDeadline.RemainingRealSeconds, Is.EqualTo(40d).Within(Fixtures.Tolerance), "paused time does not count");
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));

                session.Tick(45d);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(session.PlanningDeadline.HasExpired, Is.True);
                Assert.That(session.PlanningDeadline.RemainingRealSeconds, Is.EqualTo(0d));
                Assert.That(session.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.False, "the draft itself was valid");
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(4));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(0d), "the launch frame spends no simulated time");
                Assert.That(recorder.Phases, Has.Count.EqualTo(1));

                session.Tick(1d);
                Assert.That(recorder.Phases, Has.Count.EqualTo(1), "the deadline launches only once");
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(1d).Within(Fixtures.Tolerance));
            }
        }

        [Test]
        public void InvalidDraftAtZero_FallsBackToTheLastValidLoadout_AndRecordsIt()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(session.PlanningDeadline.TryEnable(30d).Succeeded, Is.True);
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 3).Succeeded, Is.True, "still valid: 571 kg, 765 credits");
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SolarWing, 0).Succeeded, Is.True, "now invalid: no power for the spectrometer");
                Assert.That(session.Planner.Draft.Validate().Succeeded, Is.False);
                IMissionConfiguration lastValid = session.PlanningDeadline.LastValidConfiguration;

                session.Tick(30d);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(session.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.True);
                Assert.That(session.PlanningDeadline.DeadlineLaunchResult.Value.Succeeded, Is.True);
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(3), "the player's last valid choice, not a default preset");
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.SolarWing), Is.EqualTo(1));
                Assert.That(session.Planner.LaunchedConfiguration.TotalMassKg, Is.EqualTo(lastValid.TotalMassKg));
                Assert.That(session.Planner.Draft.Validate().Succeeded, Is.True, "the draft shows what was launched");
            }
        }

        [Test]
        public void ManualLaunchBeforeZero_StopsTheDeadline()
        {
            using (var session = Fixtures.NewSession())
            {
                Assert.That(session.PlanningDeadline.TryEnable().Succeeded, Is.True);
                session.Tick(10d);
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);

                session.Tick(1000d);

                Assert.That(session.PlanningDeadline.HasExpired, Is.False);
                Assert.That(session.PlanningDeadline.RemainingRealSeconds, Is.EqualTo(50d).Within(Fixtures.Tolerance));
                Assert.That(session.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.False);
            }
        }

        [Test]
        public void Enable_IsValidated()
        {
            using (var session = Fixtures.NewSession())
            {
                Assert.That(session.PlanningDeadline.TryEnable(0d).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.PlanningDeadline.TryEnable(double.NaN).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.PlanningDeadline.IsEnabled, Is.False);

                Assert.That(session.PlanningDeadline.TryEnable().Succeeded, Is.True);
                Assert.That(session.PlanningDeadline.TryEnable().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
            }

            using (var launched = Fixtures.LaunchedSession())
            {
                Assert.That(launched.PlanningDeadline.TryEnable().Failure, Is.EqualTo(CommandFailure.WrongPhase));
            }
        }

        [Test]
        public void TimedPlanning_FailsExplicitly_WithoutAnyValidLoadoutToFallBackTo()
        {
            MissionDefinition aurora = AuroraMission.CreateDefinition();
            var emptyStart = new MissionDefinition(aurora.Catalog, aurora.Parameters, aurora.CrewCount, null, null, aurora.Scenario);
            using (var session = new MissionSession(emptyStart))
            {
                Assert.That(session.Planner.Draft.Validate().Succeeded, Is.False, "an empty loadout lacks supplies");

                CommandResult enable = session.PlanningDeadline.TryEnable();

                Assert.That(enable.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(session.PlanningDeadline.IsEnabled, Is.False);
                Assert.That(session.PlanningDeadline.LastValidConfiguration, Is.Null);
            }
        }

        [Test]
        public void ExpiryAndManualLaunch_CanNeverBothLaunch()
        {
            using (var session = Fixtures.NewSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.PlanningDeadline.TryEnable(10d).Succeeded, Is.True);
                session.Tick(10d);
                CommandResult lateClick = session.Planner.TryLaunch();

                Assert.That(lateClick.Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "the expiry launched first; the click in the same frame is rejected");
                Assert.That(recorder.Phases, Has.Count.EqualTo(1));
                Assert.That(recorder.Resources, Has.Count.EqualTo(1), "reserves were loaded once");
            }

            using (var session = Fixtures.NewSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.PlanningDeadline.TryEnable(10d).Succeeded, Is.True);
                session.Tick(9.99d);
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True, "the player launches just before zero");
                session.Tick(1d);

                Assert.That(session.PlanningDeadline.HasExpired, Is.False, "the deadline no longer acts after a manual launch");
                Assert.That(session.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.False);
                Assert.That(recorder.Phases, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void ClockAndDeadline_ImplementTheSharedContracts_WithTheirDocumentedSemantics()
        {
            using (var session = Fixtures.EmergencySession())
            {
                ISimulationClock clock = session.ClockView;
                IPlanningDeadline deadline = session.PlanningDeadline;
                IGameState state = session.State;
                session.AdvanceSimulation(120d);

                Assert.That(clock.ElapsedSimulationSeconds, Is.EqualTo(state.ElapsedSimulationSeconds), "delegates to mission state, no second counter");
                Assert.That(clock.TimeScale, Is.EqualTo(300d));
                Assert.That(clock.IsPaused, Is.False);

                Assert.That(session.Clock.Pause().Succeeded, Is.True);
                Assert.That(clock.IsPaused, Is.True);
                Assert.That(clock.TimeScale, Is.EqualTo(300d), "the time scale keeps its value while paused");

                Assert.That(deadline.IsEnabled, Is.False, "practice planning was used");
                Assert.That(deadline.RemainingRealSeconds, Is.EqualTo(0d));
                Assert.That(deadline.UsedLastValidLoadoutAtDeadline, Is.False);
                Assert.That(deadline.LastValidConfiguration, Is.Not.Null);
                Assert.That(deadline.LastValidConfiguration.Validate().Succeeded, Is.True);
            }
        }
    }
}
