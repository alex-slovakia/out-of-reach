using System;
using System.Collections;
using NUnit.Framework;
using OutOfReach.Composition;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Interaction;
using UnityEngine;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// CLAUDE-INTEGRATION (QA-E2E finding 2): the real I06HabitatBindings decides whether the galley and berth targets
    /// can be used. With the opt-in chapter loop, Core's day-1 prerequisites (first ration, one completed rest) must be
    /// reachable through those bindings in the cabin phases before contact, and each command must debit exactly once.
    /// Without the chapter loop the habitat stays hidden before Survival, as before.
    /// EXPLICIT SHORTCUT, and only this: the impact and the two interior repairs go through domain commands
    /// (RepairLoopHarness.ReachStabilized, covered by its own tests).
    /// </summary>
    public sealed class HabitatChapterExposurePlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const double RealStep = 0.1d;

        private RepairLoopHarness harness;
        private I06HabitatBindings habitat;

        private MissionSession Session => harness.Session;

        private void Build(bool chapterLoop)
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build(survival: true);
            harness.Runtime.ConfigureChapterLoop(chapterLoop);
            harness.Runtime.StartSession();
            harness.Runtime.DriveFromUpdate = false;
            habitat = new GameObject("Habitat bindings under test").AddComponent<I06HabitatBindings>();
            habitat.ConfigureViews(null, null, new[] { harness.Ration, harness.Water, harness.Sleep });
            habitat.ConfigureSession(harness.Runtime);
        }

        [TearDown]
        public void TearDown()
        {
            if (habitat) UnityEngine.Object.DestroyImmediate(habitat.gameObject);
            harness?.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static bool Usable(InteractableTarget target)
        {
            foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
                if (collider.enabled) return true;
            return false;
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
                if (i % 10 == 9) yield return null;
            }
            Assert.That(done(), Is.True, what + " within " + maxRealSeconds.ToString("0.#") + " real seconds");
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ChapterLoop_DayOneMealAndRest_AreReachableThroughTheHabitat_AndDebitOnce()
        {
            Build(chapterLoop: true);
            Assert.That(harness.Runtime.ChapterFlow, Is.Not.Null, "the runtime built a chapter session");
            yield return Frames(2);
            Assert.That(Usable(harness.Ration) || Usable(harness.Sleep), Is.False, "nothing is exposed during planning");

            harness.ReachStabilized();
            yield return Frames(2);
            GamePhase phase = Session.State.Phase;
            Assert.That(phase == GamePhase.Emergency || phase == GamePhase.Repair || phase == GamePhase.Transmission, Is.True, "cabin phase " + phase);
            Assert.That(Usable(harness.Ration) && Usable(harness.Water) && Usable(harness.Sleep), Is.True,
                "galley and berth are exposed in the chapter loop's cabin phase " + phase);

            double food = Session.Resources.Snapshot.FoodKg;
            yield return Use(harness.Ration);
            Assert.That(Session.Survival.RationsEaten, Is.EqualTo(1), "one packaged meal");
            Assert.That(Session.Resources.Snapshot.FoodKg, Is.EqualTo(food - Session.Survival.RationFoodKg).Within(1e-9), "debited once");

            yield return Use(harness.Sleep);
            Assert.That(Session.SurvivalRecovery.IsResting, Is.True, "the berth starts Core's rest");
            yield return RunUntil(() => !Session.SurvivalRecovery.IsResting, 30d, "the rest completes");
            Assert.That(Session.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
            Assert.That(Session.SurvivalRecovery.CompletedRestCount, Is.EqualTo(1), "exactly one completed rest");
            // The ledger keeps its ordinary metabolic food draw while the rest's hours pass (measured 0.648 kg over the
            // night); the rest itself issues no ration command, so the packaged-meal count stays at one.
            Assert.That(Session.Survival.RationsEaten, Is.EqualTo(1), "the rest itself ate nothing");
            Assert.That(harness.Runtime.ChapterFlow.CheckContinueRoutine().Succeeded, Is.True,
                "Core's day-1 prerequisites are met: " + harness.Runtime.ChapterFlow.CheckContinueRoutine().Explanation);
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator LegacyLoop_KeepsTheHabitatHiddenUntilSurvival()
        {
            Build(chapterLoop: false);
            Assert.That(harness.Runtime.ChapterFlow, Is.Null, "no chapter session without the switch");
            harness.ReachStabilized();
            yield return Frames(2);
            Assert.That(Usable(harness.Ration) || Usable(harness.Water) || Usable(harness.Sleep), Is.False,
                "Survival-mode behaviour unchanged: no galley or berth in " + Session.State.Phase);
        }
    }
}
