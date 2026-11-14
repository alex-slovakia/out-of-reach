using System;
using NUnit.Framework;
using OutOfReach.Contracts;

namespace OutOfReach.Tests.EditMode
{
    public sealed class GameStateTests
    {
        [Test]
        public void NewMission_StartsInPlanning_WithNoOutcomeAndNoTime()
        {
            using (var session = Fixtures.NewSession())
            {
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(0d));
            }
        }

        [Test]
        public void PublicTransitionToIntro_IsRejected_BecauseLaunchMustValidateTheLoadout()
        {
            using (var session = Fixtures.NewSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                CommandResult result = session.State.TryTransition(GamePhase.Intro);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
                Assert.That(recorder.Total, Is.EqualTo(0));
            }
        }

        [TestCase(GamePhase.Emergency)]
        [TestCase(GamePhase.Repair)]
        [TestCase(GamePhase.Transmission)]
        [TestCase(GamePhase.Report)]
        public void IllegalTransitionFromPlanning_LeavesStateUnchanged(GamePhase target)
        {
            using (var session = Fixtures.NewSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                CommandResult result = session.State.TryTransition(target);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
                Assert.That(recorder.Total, Is.EqualTo(0));
            }
        }

        [Test]
        public void FullLegalPath_PublishesEachPhaseChangeExactlyOnce()
        {
            using (var session = Fixtures.NewSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                Assert.That(session.State.TryTransition(GamePhase.Emergency).Succeeded, Is.True);
                Assert.That(session.State.TryTransition(GamePhase.Repair).Succeeded, Is.True);
                Assert.That(session.State.TryTransition(GamePhase.Eva).Succeeded, Is.True);
                Assert.That(session.State.TryTransition(GamePhase.Repair).Succeeded, Is.True, "return inside before the antenna is fixed");
                Assert.That(session.State.TryTransition(GamePhase.Eva).Succeeded, Is.True);
                Assert.That(session.State.TryTransition(GamePhase.Transmission).Succeeded, Is.True);
                Assert.That(session.State.TryEndMission(MissionOutcome.RecoveryPossible, "Contact restored.", session.Resources.Snapshot).Succeeded, Is.True);
                Assert.That(session.State.TryTransition(GamePhase.Report).Succeeded, Is.True);

                var expected = new[]
                {
                    new PhaseChanged(GamePhase.Planning, GamePhase.Intro),
                    new PhaseChanged(GamePhase.Intro, GamePhase.Emergency),
                    new PhaseChanged(GamePhase.Emergency, GamePhase.Repair),
                    new PhaseChanged(GamePhase.Repair, GamePhase.Eva),
                    new PhaseChanged(GamePhase.Eva, GamePhase.Repair),
                    new PhaseChanged(GamePhase.Repair, GamePhase.Eva),
                    new PhaseChanged(GamePhase.Eva, GamePhase.Transmission),
                    new PhaseChanged(GamePhase.Transmission, GamePhase.Outcome),
                    new PhaseChanged(GamePhase.Outcome, GamePhase.Report),
                };
                Assert.That(recorder.Phases, Has.Count.EqualTo(expected.Length));
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.That(recorder.Phases[i].Previous, Is.EqualTo(expected[i].Previous), $"event {i} previous");
                    Assert.That(recorder.Phases[i].Current, Is.EqualTo(expected[i].Current), $"event {i} current");
                }

                Assert.That(recorder.Endings, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void RepeatedTransition_IsRejected_WithoutDuplicateEvent()
        {
            using (var session = Fixtures.LaunchedSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.State.TryTransition(GamePhase.Emergency).Succeeded, Is.True);
                CommandResult repeat = session.State.TryTransition(GamePhase.Emergency);

                Assert.That(repeat.Succeeded, Is.False);
                Assert.That(repeat.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(recorder.Phases, Has.Count.EqualTo(1));
            }
        }

        [TestCase(GamePhase.Repair, Description = "Intro cannot skip the emergency")]
        [TestCase(GamePhase.Eva, Description = "Intro cannot jump to EVA")]
        [TestCase(GamePhase.Planning, Description = "No rewind after launch")]
        [TestCase(GamePhase.Report, Description = "No report without an outcome")]
        public void IllegalTransitionAfterLaunch_IsRejected(GamePhase target)
        {
            using (var session = Fixtures.LaunchedSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                CommandResult result = session.State.TryTransition(target);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(recorder.Total, Is.EqualTo(0));
            }
        }

        [Test]
        public void UndefinedPhaseValue_IsInvalidInput()
        {
            using (var session = Fixtures.NewSession())
            {
                CommandResult result = session.State.TryTransition((GamePhase)99);

                Assert.That(result.Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
            }
        }

        [Test]
        public void Outcome_IsDecidedOnlyByCore_OnceWithReasonAndEvents()
        {
            using (var session = Fixtures.LaunchedSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                CommandResult direct = session.State.TryTransition(GamePhase.Outcome);
                Assert.That(direct.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));

                ResourceSnapshot final = session.Resources.Snapshot;
                CommandResult ended = session.State.TryEndMission(MissionOutcome.Lost, "Oxygen reserve depleted.", final);
                CommandResult again = session.State.TryEndMission(MissionOutcome.RecoveryPossible, "late success", final);

                Assert.That(ended.Succeeded, Is.True, ended.Explanation);
                Assert.That(again.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.State.OutcomeReason, Is.EqualTo("Oxygen reserve depleted."));
                Assert.That(recorder.Order, Is.EqualTo(new[] { "PhaseChanged", "MissionEnded" }));
                Assert.That(recorder.Endings[0].Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(recorder.Endings[0].Reason, Is.EqualTo("Oxygen reserve depleted."));

                Assert.That(session.State.TryTransition(GamePhase.Report).Succeeded, Is.True);
                Assert.That(session.State.TryTransition(GamePhase.Planning).Failure, Is.EqualTo(CommandFailure.WrongPhase), "Report is terminal; restart builds a new session");
            }
        }

        [Test]
        public void EndingWithNone_OrBeforeLaunch_IsRejected()
        {
            using (var planning = Fixtures.NewSession())
            using (var launched = Fixtures.LaunchedSession())
            {
                Assert.That(planning.State.TryEndMission(MissionOutcome.Lost, "x", default(ResourceSnapshot)).Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(launched.State.TryEndMission(MissionOutcome.None, "x", default(ResourceSnapshot)).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(launched.State.Outcome, Is.EqualTo(MissionOutcome.None));
            }
        }

        [Test]
        public void AdvanceTime_RejectsInvalidValues_WithoutMutation([Values(-1d, double.NaN, double.PositiveInfinity)] double invalid)
        {
            using (var session = Fixtures.LaunchedSession())
            {
                session.State.AdvanceTime(10d);

                Assert.Throws<ArgumentOutOfRangeException>(() => session.State.AdvanceTime(invalid));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(10d));
            }
        }
    }
}
