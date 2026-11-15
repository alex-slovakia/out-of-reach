using System;
using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Interaction;
using UnityEngine;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// CORE-CHAPTER-LOOP through the real MissionRuntime in the grey-box harness: the day-1 meal and sleep by E on the
    /// harness targets, then the routine to day 5 in real-time runtime frames (0.1 s each), with player control off
    /// during the routine and back afterwards; disabling the runtime stops a running routine and restores the speed.
    /// EXPLICIT SHORTCUTS, and only these: the impact and the two interior repairs go through domain commands
    /// (RepairLoopHarness.ReachStabilized, covered by its own tests).
    /// </summary>
    public sealed class ChapterLoopPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const double RealStep = 0.1d;

        private RepairLoopHarness harness;

        private MissionSession Session => harness.Session;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build(survival: true);
            harness.Runtime.ConfigureChapterLoop(true);
            harness.Runtime.StartSession();
            harness.Runtime.DriveFromUpdate = false;
        }

        [TearDown]
        public void TearDown()
        {
            harness.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private IEnumerator Use(InteractableTarget target)
        {
            Vector3 at = target.transform.position;
            harness.PlacePlayerFacing(at.x - Mathf.Sign(at.x), at.z, at);
            yield return Frames(3);
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(target), "aimed at " + target.name);
            Assert.That(harness.Resolver.CurrentCanInteract, Is.True, target.name + " prompt: " + harness.Resolver.CurrentPrompt);
            harness.Input.PressInteract();
            yield return Frames(3);
            CommandResult? last = harness.Player.LastInteraction;
            Assert.That(last.HasValue && last.Value.Succeeded, Is.True, target.name + ": " + (last.HasValue ? last.Value.Explanation : "no interaction"));
        }

        private IEnumerator RunUntil(Func<bool> done, double maxRealSeconds, string what)
        {
            int frames = (int)Math.Ceiling(maxRealSeconds / RealStep);
            for (int i = 0; i < frames && !done(); i++)
            {
                harness.Runtime.Step(RealStep);
                if (i % 10 == 9)
                {
                    yield return null;
                }
            }

            Assert.That(done(), Is.True, what + " within " + maxRealSeconds.ToString("0.#") + " real seconds");
        }

        /// <summary>Alex's day 1: stabilized, the antenna EVA and Earth's reply (domain shortcut); a meal through its target.</summary>
        private IEnumerator CompleteDayOne()
        {
            Assert.That(harness.Runtime.ChapterFlow, Is.Not.Null, "the runtime built a chapter session");
            harness.ReachContact();
            Assert.That(harness.Runtime.ChapterFlow.Rescue, Is.EqualTo(RescuePlan.FirstRescuePlanned), "the reply: rescue expected on day 14");
            yield return Use(harness.Ration);
            Assert.That(Session.Survival.RationsEaten, Is.EqualTo(1), "first packaged meal through its target");
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator DayOneThroughTheTargets_ThenTheRoutineReachesDayFive_InRuntimeFrames()
        {
            yield return CompleteDayOne();
            IMissionChapterFlow flow = harness.Runtime.ChapterFlow;
            double interiorScale = Session.Definition.Scenario.Pacing.InteriorTimeScale;
            double jumped = Session.JumpedSimulationSeconds;

            // CHAPTER-PACING-01: E on the berth is the story sleep: the night begins and the routine runs to day 5.
            yield return Use(harness.Sleep);
            Assert.That(flow.IsRoutineRunning, Is.True, "the berth started the routine");
            Assert.That(Session.SurvivalRecovery.IsResting, Is.True, "its first night begins at once");
            Assert.That(Session.IsPlayerControlAllowed, Is.False, "no walking during the montage");
            harness.Resolver.Refresh();
            Assert.That(Session.ChapterFlow.TrySleepInteraction().Succeeded, Is.False, "a second press cannot start a second skip");
            yield return RunUntil(() => !flow.IsRoutineRunning, 60d, "the routine stops");

            Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.ReachedChapter), flow.LastRoutineStopText);
            Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Consequences));
            Assert.That(Session.SurvivalRecovery.MissionDay, Is.EqualTo(5));
            Assert.That(flow.RoutineRests, Is.GreaterThanOrEqualTo(3));
            Assert.That(Session.JumpedSimulationSeconds, Is.EqualTo(jumped), "no jump: the routine ran in frames");
            Assert.That(Session.Clock.TimeScale, Is.EqualTo(interiorScale));
            Assert.That(Session.IsPlayerControlAllowed, Is.True, "control returns at the beat");
            Assert.That(flow.PendingDecision, Is.EqualTo(ChapterDecision.None), "day 5 is the chores checklist");
            Assert.That(flow.CheckContinueRoutine().Explanation, Does.Contain("Day-5 chores"));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator DisablingTheRuntimeStopsARunningRoutine_AndRestoresTheSpeed()
        {
            yield return CompleteDayOne();
            IMissionChapterFlow flow = harness.Runtime.ChapterFlow;
            Assert.That(flow.TryContinueRoutine().Succeeded, Is.True);
            yield return RunUntil(() => Session.SurvivalRecovery.IsResting, 30d, "the routine's first night");

            harness.Runtime.enabled = false;
            yield return null;
            Assert.That(flow.IsRoutineRunning, Is.False);
            Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.PlayerCancelled));
            Assert.That(Session.SurvivalRecovery.IsResting, Is.False);
            Assert.That(Session.Clock.TimeScale, Is.EqualTo(Session.Definition.Scenario.Pacing.InteriorTimeScale));
        }
    }
}
