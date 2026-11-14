using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// Codex 6881239000 / 6881374227: really disabling and re-enabling the MissionRuntime while the crew rests. The
    /// disabled runtime wakes the crew (Woken) and restores the rate from before the rest, even while paused; it drives
    /// nothing while disabled. Re-enabled, the same session continues awake and the player's input works again.
    /// Pause alone keeps a rest.
    /// </summary>
    public sealed class SurvivalRecoveryLifecyclePlayTests
    {
        private const float FrameSeconds = 0.02f;

        private RepairLoopHarness harness;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build();
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

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        private static void Acknowledge(MissionSession session)
        {
            if (session.Cinematics.IsWaiting)
            {
                session.Events.Publish(new CinematicFinished(session.Cinematics.ActiveCinematic, session.Cinematics.ActiveSequenceId, false));
            }
        }

        /// <summary>A fresh session with the survival extension, taken through the domain to Earth's reply (Survival).</summary>
        private MissionSession ReachSurvival()
        {
            harness.Runtime.ConfigureSurvivalExtension(true);
            harness.Runtime.StartSession();
            MissionSession session = harness.Session;
            Assert.That(session.SurvivalRecovery, Is.Not.Null);

            harness.ReachStabilized();
            Require(session.Eva.TryPrepareSuit());
            Require(session.Eva.TryBeginEva());
            Require(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds));
            Require(session.Eva.TryGoOutside());
            Require(session.Systems.Antenna.TryRepair(session.Resources));
            Require(session.Eva.TryReturnInside());
            Require(session.Eva.TryEndEva());
            Require(session.AdvanceSimulation(session.Eva.Spec.RepressurizationSeconds));
            Require(session.Director.TryRunLinkDiagnostic());
            Acknowledge(session);
            Require(session.Communications.TryTransmit());
            Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
            return session;
        }

        [UnityTest]
        public IEnumerator DisablingTheRuntime_WakesARestingCrew_AndReenablingContinuesTheSameSessionAwake()
        {
            MissionSession session = ReachSurvival();
            SurvivalRecovery recovery = session.SurvivalRecovery;
            double survivalScale = session.Clock.TimeScale;
            Vector3 console = harness.Console.transform.position;
            harness.PlacePlayerFacing(0f, console.z - 0.15f - 1.5f, console);

            Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
            harness.Runtime.DriveFromUpdate = true;
            double restStart = session.State.ElapsedSimulationSeconds;
            yield return Frames(5);
            Assert.That(recovery.IsResting, Is.True);
            Assert.That(session.State.ElapsedSimulationSeconds, Is.GreaterThan(restStart), "the runtime drives the rest");
            Assert.That(session.Clock.TimeScale, Is.EqualTo(SurvivalRecovery.RestTimeScale));

            harness.Runtime.enabled = false;
            Assert.That(recovery.IsResting, Is.False, "disabling the runtime wakes the crew");
            Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Woken));
            Assert.That(session.Clock.TimeScale, Is.EqualTo(survivalScale), "no 3,600x scale outlives the runtime");
            double atDisable = session.State.ElapsedSimulationSeconds;
            yield return Frames(5);
            Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(atDisable), "a disabled runtime drives nothing");

            harness.Runtime.enabled = true;
            yield return Frames(5);
            Assert.That(harness.Session, Is.SameAs(session), "the same session continues");
            Assert.That(session.State.ElapsedSimulationSeconds, Is.GreaterThan(atDisable), "driven again after re-enabling");
            Assert.That(recovery.IsResting, Is.False, "awake");
            Assert.That(session.Clock.TimeScale, Is.EqualTo(survivalScale));
            Assert.That(harness.Player.ControlAllowed, Is.True);

            Vector3 before = harness.Player.transform.position;
            harness.Input.Move = new Vector2(0f, 1f);
            yield return Frames(10);
            harness.Input.Move = Vector2.zero;
            Assert.That(Vector3.Distance(harness.Player.transform.position, before), Is.GreaterThan(0.05f), "player input works again");

            Require(recovery.TryBeginRest(1d));
            Assert.That(recovery.IsResting, Is.True, "a new rest works");
        }

        [UnityTest]
        public IEnumerator DisablingThePausedRuntime_WakesTheRestingCrew_KeepsThePause_AndResumeContinues()
        {
            MissionSession session = ReachSurvival();
            SurvivalRecovery recovery = session.SurvivalRecovery;
            double survivalScale = session.Clock.TimeScale;

            Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
            harness.Runtime.DriveFromUpdate = true;
            yield return Frames(3);
            harness.Runtime.SetPaused(true);
            yield return Frames(3);
            Assert.That(recovery.IsResting, Is.True, "pause alone keeps the rest");
            Assert.That(session.Clock.TimeScale, Is.EqualTo(SurvivalRecovery.RestTimeScale));
            double frozen = session.State.ElapsedSimulationSeconds;
            double remaining = recovery.RestRemainingSimulationSeconds;
            yield return Frames(3);
            Assert.That(recovery.RestRemainingSimulationSeconds, Is.EqualTo(remaining), "paused: the rest is frozen");

            harness.Runtime.enabled = false;
            Assert.That(recovery.IsResting, Is.False);
            Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Woken));
            Assert.That(session.Clock.IsPaused, Is.True, "the pause itself is untouched");
            Assert.That(session.Clock.TimeScale, Is.EqualTo(survivalScale));

            harness.Runtime.enabled = true;
            yield return Frames(3);
            Assert.That(harness.Session, Is.SameAs(session));
            Assert.That(session.Clock.IsPaused, Is.True, "still paused after re-enabling");
            Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(frozen));
            Assert.That(harness.Player.ControlAllowed, Is.False, "no control while paused");

            harness.Runtime.SetPaused(false);
            yield return Frames(3);
            Assert.That(session.State.ElapsedSimulationSeconds, Is.GreaterThan(frozen), "resumed, awake");
            Assert.That(harness.Player.ControlAllowed, Is.True);
            Require(recovery.TryBeginRest(1d));
            Assert.That(recovery.IsResting, Is.True, "a new rest works");
        }
    }
}
