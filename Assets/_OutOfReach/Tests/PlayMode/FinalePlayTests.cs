using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>The finale in the grey-box harness: the console interaction, the wait driven by frames, the report and a restart.</summary>
    public sealed class FinalePlayTests
    {
        private const float FrameSeconds = 0.02f;

        private RepairLoopHarness harness;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build();
            // Simulated time is advanced explicitly; the runtime still syncs the scene every frame.
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

        private IEnumerator PressInteract()
        {
            harness.Input.PressInteract();
            yield return Frames(3);
        }

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        /// <summary>Repairs the antenna on one EVA through the domain (the interaction path is covered by EvaPlayTests).</summary>
        private void ReachTransmission()
        {
            harness.ReachStabilized();
            MissionSession session = harness.Session;
            Require(session.Eva.TryPrepareSuit());
            Require(session.Eva.TryBeginEva());
            Require(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds));
            Require(session.Eva.TryGoOutside());
            Require(session.Systems.Antenna.TryRepair(session.Resources));
            Require(session.Eva.TryReturnInside());
            Require(session.Eva.TryEndEva());
            Require(session.AdvanceSimulation(session.Eva.Spec.RepressurizationSeconds));
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Transmission));
        }

        [UnityTest]
        public IEnumerator Finale_ThroughTheConsole_WaitsTheLightTime_ReachesTheReport_AndRestartsClean()
        {
            ReachTransmission();
            MissionSession session = harness.Session;
            yield return Frames(3);
            Assert.That(harness.Player.transform.position.z, Is.GreaterThan(RepairLoopHarness.InnerGateZ), "back in the cabin after the EVA");

            Vector3 console = RepairLoopHarness.ConsolePosition;
            harness.PlacePlayerFacing(0f, console.z - 0.15f - 1.5f, console);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.Console));
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Run communications diagnostic"));
            Assert.That(harness.Runtime.Communications.State, Is.EqualTo(CommunicationState.Ready));

            // 1. The false signal: Core requests the sequence; with no presentation it completes on the next step.
            yield return PressInteract();
            Assert.That(session.Director.LinkDiagnosticRequested, Is.True);
            Assert.That(harness.Runtime.Communications.State, Is.EqualTo(CommunicationState.Ready), "the replay is not a reply");
            Require(harness.Runtime.Step(FrameSeconds));
            yield return Frames(2);
            Assert.That(session.Director.LinkDiagnosticCompleted, Is.True);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Transmit distress call: 0.5 kWh. Earth is 197,708,000 km away; earliest reply in 31 min 59 s."));
            Assert.That(harness.Resolver.CurrentCanInteract, Is.True);

            // 2. The one transmission.
            double battery = session.Resources.Snapshot.BatteryKWh;
            yield return PressInteract();
            Assert.That(harness.Runtime.Communications.State, Is.EqualTo(CommunicationState.AwaitingReply));
            Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery - 0.5d).Within(1e-9));
            Assert.That(harness.Runtime.Communications.ReferenceDistanceKm, Is.EqualTo(197707975.990852982d).Within(1e-6), "NASA/JPL row of the mission date");
            yield return Frames(2);
            Assert.That(harness.Resolver.CurrentCanInteract, Is.False);
            Assert.That(harness.Resolver.CurrentPrompt, Does.StartWith("Awaiting reply from Earth: "));
            yield return PressInteract();
            Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery - 0.5d).Within(1e-9), "pressing again sends nothing");

            // 3. The wait, driven like frames at the disclosed x60 scale: about 32 real seconds.
            double due = session.Communications.ReplyDueAtSimulationSeconds.Value;
            Assert.That(harness.Runtime.Clock.TimeScale, Is.EqualTo(60d));
            int steps = 0;
            while (session.State.Outcome == MissionOutcome.None && steps < 400)
            {
                Require(harness.Runtime.Step(0.25d));
                steps++;
            }

            Assert.That(steps, Is.EqualTo(128));
            Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
            Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(due).Within(1e-6));
            Assert.That(harness.Runtime.Communications.State, Is.EqualTo(CommunicationState.ReplyReceived));
            Assert.That(session.IsPlayerControlAllowed, Is.False, "the mission is over");

            // 4. Ending sequence without presentation, then the report.
            Require(harness.Runtime.Step(FrameSeconds));
            Require(harness.Runtime.Step(FrameSeconds));
            yield return Frames(2);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report));
            MissionReport report = harness.Runtime.Report;
            Assert.That(report, Is.Not.Null);
            Assert.That(report.IsFinal, Is.True);
            Assert.That(report.Headline, Is.EqualTo("RECOVERY POSSIBLE"));
            Assert.That(report.Communication.DatasetId, Is.EqualTo("jpl-horizons-earth-mars-2026-v1"));
            Assert.That(report.PlayedRealSeconds, Is.GreaterThan(32d - 1e-6), "the frames of the wait were counted");

            // 5. Restart: a new mission with nothing left over.
            harness.Runtime.RestartMission();
            yield return Frames(3);
            Assert.That(harness.Session, Is.Not.SameAs(session));
            Assert.That(session.IsDisposed, Is.True);
            Assert.That(harness.Session.State.Phase, Is.EqualTo(GamePhase.Planning));
            Assert.That(harness.Runtime.Report, Is.Null);
            Assert.That(harness.Runtime.Communications.State, Is.EqualTo(CommunicationState.Ready));
            Assert.That(harness.Session.Communications.HasTransmitted, Is.False);
            Assert.That(harness.Listener.IsBound, Is.True, "views rebound to the new mission");
        }

        [UnityTest]
        public IEnumerator SkipSimulatedTime_AdvancesReservesAndTheReplyClockTogether()
        {
            ReachTransmission();
            MissionSession session = harness.Session;
            Require(session.Director.TryRunLinkDiagnostic());
            Require(harness.Runtime.Step(FrameSeconds));
            Require(harness.Runtime.Communications.TryTransmit());
            double remaining = harness.Runtime.Communications.ReplyRemainingSimulationSeconds;
            double water = session.Resources.Snapshot.WaterLitres;

            Require(harness.Runtime.SkipSimulatedTime(600d));
            yield return Frames(2);

            Assert.That(harness.Runtime.Communications.ReplyRemainingSimulationSeconds, Is.EqualTo(remaining - 600d).Within(1e-6));
            Assert.That(session.Resources.Snapshot.WaterLitres, Is.LessThan(water));
            Assert.That(harness.Runtime.Calendar.NowUtc, Is.GreaterThan(harness.Runtime.Calendar.EpochAtStartUtc));

            harness.Runtime.SetPaused(true);
            Assert.That(harness.Runtime.SkipSimulatedTime(600d).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "no skip while paused");
            harness.Runtime.SetPaused(false);

            Require(harness.Runtime.SkipSimulatedTime(86400d));
            Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible), "the skip stops at the reply");
            Assert.That(harness.Runtime.SkipSimulatedTime(1d).Failure, Is.EqualTo(CommandFailure.WrongPhase));
        }
    }
}
