using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    public sealed class EvaPlayTests
    {
        private const float FrameSeconds = 0.02f;

        private RepairLoopHarness harness;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build();
            // Simulated time is advanced explicitly in these tests; the runtime still syncs gates and the route.
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

        private IEnumerator Walk(int frames)
        {
            harness.Input.Move = new Vector2(0f, 1f);
            yield return Frames(frames);
            harness.Input.Move = Vector2.zero;
        }

        private IEnumerator PressInteract()
        {
            harness.Input.PressInteract();
            yield return Frames(3);
        }

        private float DistanceFromRoute()
        {
            Vector3 onLine = RouteMath.ClosestPoint(RepairLoopHarness.RouteWaypoints, harness.Player.transform.position, out float unused);
            return Vector3.Distance(onLine, harness.Player.transform.position);
        }

        [UnityTest]
        public IEnumerator InnerGate_FollowsTheAirlock_AndTheOuterHatchIsNeverWalkable()
        {
            harness.ReachStabilized();
            yield return Frames(3);
            Assert.That(harness.InnerGate.IsOpen, Is.True, "cabin and airlock are connected while the airlock is InnerOpen");
            Assert.That(harness.OuterGate.IsOpen, Is.False);

            harness.PlacePlayerFacing(0f, -2.2f, new Vector3(0f, 0f, -10f));
            yield return Walk(60);
            Assert.That(harness.Player.transform.position.z, Is.LessThan(RepairLoopHarness.InnerGateZ - 0.5f), "walked through the open inner gate");

            Assert.That(harness.Session.Eva.TryPrepareSuit().Succeeded, Is.True);
            Assert.That(harness.Session.Eva.TryBeginEva().Succeeded, Is.True);
            yield return Frames(3);
            Assert.That(harness.InnerGate.IsOpen, Is.False, "sealed for depressurization");
            Assert.That(harness.InnerGate.Blocker.enabled, Is.True);

            harness.PlacePlayerFacing(0f, -4.5f, new Vector3(0f, 0f, 10f));
            yield return Walk(100);
            Assert.That(harness.Player.transform.position.z, Is.LessThan(RepairLoopHarness.InnerGateZ - 0.3f), "the closed inner gate holds");

            Assert.That(harness.Session.AdvanceSimulation(harness.Session.Eva.Spec.DepressurizationSeconds).Succeeded, Is.True);
            yield return Frames(3);
            Assert.That(harness.Session.Eva.Airlock, Is.EqualTo(AirlockState.OuterOpen));
            Assert.That(harness.OuterGate.IsOpen, Is.False, "the outer hatch is crossed only through the exit interaction");
            Assert.That(harness.InnerGate.IsOpen, Is.False, "the two hatches are never open together");

            harness.PlacePlayerFacing(0f, -5.5f, new Vector3(0f, 0f, -20f));
            yield return Walk(100);
            Assert.That(harness.Player.transform.position.z, Is.GreaterThan(RepairLoopHarness.OuterGateZ + 0.3f), "nobody walks into vacuum");
            Assert.That(harness.Player.IsOnRoute, Is.False);
        }

        [UnityTest]
        public IEnumerator GoingOutside_IsRefused_WithoutTheSuitAndTheAirlockCycle()
        {
            harness.ReachStabilized();
            harness.PlacePlayerFacing(0.9f, -5.6f, RepairLoopHarness.EvaExitPosition);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.EvaExit));
            Assert.That(harness.Resolver.CurrentCanInteract, Is.False);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("A sealed suit and a depressurized airlock are required to go outside."));

            yield return PressInteract();

            Assert.That(harness.Player.LastInteraction.Value.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(harness.Session.Eva.IsOutside, Is.False);
            Assert.That(harness.Player.IsOnRoute, Is.False);
        }

        [UnityTest]
        public IEnumerator FullEva_ThroughInteractions_RepairsTheAntenna_AndReturnsToCabinAir()
        {
            harness.ReachStabilized();
            MissionSession session = harness.Session;

            // 1. Suit up at the locker.
            harness.PlacePlayerFacing(RepairLoopHarness.SuitLockerPosition.x + 0.15f + 1.5f, RepairLoopHarness.SuitLockerPosition.z, RepairLoopHarness.SuitLockerPosition);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Put on EVA suit: 0.84 kg oxygen, 2 h 20 min of preparation."));
            yield return PressInteract();
            Assert.That(session.Eva.IsSuitOn, Is.True);

            // 2. Inside the airlock: seal and depressurize.
            harness.PlacePlayerFacing(0f, RepairLoopHarness.AirlockControlsPosition.z, RepairLoopHarness.AirlockControlsPosition);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Seal inner hatch and depressurize: 0.11 kg oxygen, 0.2 kWh."));
            yield return PressInteract();
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Eva));
            Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.Depressurizing));
            Assert.That(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds).Succeeded, Is.True);

            // 3. Clip the tether and go outside.
            harness.PlacePlayerFacing(RepairLoopHarness.EvaExitPosition.x, -5.6f, RepairLoopHarness.EvaExitPosition);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Clip tether and go outside."));
            yield return PressInteract();
            Assert.That(session.Eva.IsOutside, Is.True);
            Assert.That(harness.Player.IsOnRoute, Is.True);
            Assert.That(harness.Player.GetComponent<CharacterController>().enabled, Is.False, "no walking physics on the tether");
            Assert.That(Vector3.Distance(harness.Player.transform.position, RepairLoopHarness.RouteWaypoints[0]), Is.LessThan(0.05f), "starts at EvaStart");

            // 4. Traverse the route; the corridor and the tether length hold.
            harness.FacePlayerTowards(RepairLoopHarness.RouteWaypoints[1]);
            yield return Walk(250);
            Assert.That(DistanceFromRoute(), Is.LessThan(harness.Route.LateralFreedomMetres + 0.01f));
            harness.FacePlayerTowards(RepairLoopHarness.RouteWaypoints[2] + new Vector3(0f, 0f, -20f));
            yield return Walk(350);
            Vector3 end = RepairLoopHarness.RouteWaypoints[2];
            Assert.That(harness.Route.DistanceAlong(harness.Player.transform.position), Is.EqualTo(harness.Route.LengthMetres).Within(0.01f), "at the end of the tether");
            Assert.That(Vector3.Distance(harness.Player.transform.position, end), Is.LessThan(harness.Route.LateralFreedomMetres + 0.01f), "cannot drift past the tether");

            // 5. Repair the antenna from the work site.
            harness.FacePlayerTowards(RepairLoopHarness.AntennaPosition);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.Antenna));
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Repair high-gain antenna: 3 spare parts, 0.5 kWh."));
            yield return PressInteract();
            Assert.That(session.Systems.Antenna.IsOperational, Is.True);
            Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(0));

            // 6. Back along the route to the hatch.
            harness.FacePlayerTowards(RepairLoopHarness.RouteWaypoints[1] + new Vector3(0f, 0f, 20f));
            yield return Walk(350);
            harness.FacePlayerTowards(RepairLoopHarness.RouteWaypoints[0] + new Vector3(-20f, 0f, 0f));
            yield return Walk(250);
            Assert.That(harness.Route.DistanceAlong(harness.Player.transform.position), Is.EqualTo(0f).Within(0.01f), "back at EvaStart");

            // 7. Enter the airlock.
            harness.FacePlayerTowards(RepairLoopHarness.EvaReturnPosition);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Enter the airlock."));
            yield return PressInteract();
            Assert.That(session.Eva.IsOutside, Is.False);
            Assert.That(harness.Player.IsOnRoute, Is.False);
            Assert.That(harness.Player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(Vector3.Distance(harness.Player.transform.position, RepairLoopHarness.ReentryPosition), Is.LessThan(0.2f), "standing inside by the outer hatch");

            // 8. Close the outer hatch, repressurize, and the mission moves on.
            harness.PlacePlayerFacing(0f, RepairLoopHarness.AirlockControlsPosition.z, RepairLoopHarness.AirlockControlsPosition);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Close outer hatch and repressurize."));
            yield return PressInteract();
            Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.Repressurizing));
            Assert.That(session.AdvanceSimulation(session.Eva.Spec.RepressurizationSeconds).Succeeded, Is.True);
            yield return Frames(3);

            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Transmission));
            Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
            Assert.That(session.Eva.IsSuitOn, Is.False);
            Assert.That(harness.InnerGate.IsOpen, Is.True, "the way back into the cabin is open");
        }

        [UnityTest]
        public IEnumerator SuitRunningEmptyOutside_EndsTheMission_AndLocksThePlayer()
        {
            harness.ReachStabilized();
            MissionSession session = harness.Session;
            Assert.That(session.Eva.TryPrepareSuit().Succeeded, Is.True);
            Assert.That(session.Eva.TryBeginEva().Succeeded, Is.True);
            Assert.That(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds).Succeeded, Is.True);
            Assert.That(session.Eva.TryGoOutside().Succeeded, Is.True);
            yield return Frames(3);
            Assert.That(harness.Player.IsOnRoute, Is.True);

            session.AdvanceSimulation(4d * 3600d);
            yield return Frames(3);

            Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
            Assert.That(session.State.OutcomeReason, Does.Contain("outside the ship"));
            Assert.That(harness.Player.ControlAllowed, Is.False);
            Vector3 position = harness.Player.transform.position;
            harness.FacePlayerTowards(RepairLoopHarness.RouteWaypoints[1]);
            yield return Walk(30);
            Assert.That(Vector3.Distance(harness.Player.transform.position, position), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator Restart_WhileOutside_PutsThePlayerBackAtTheSpawn_Walking()
        {
            harness.ReachStabilized();
            MissionSession old = harness.Session;
            Assert.That(old.Eva.TryPrepareSuit().Succeeded, Is.True);
            Assert.That(old.Eva.TryBeginEva().Succeeded, Is.True);
            Assert.That(old.AdvanceSimulation(old.Eva.Spec.DepressurizationSeconds).Succeeded, Is.True);
            Assert.That(old.Eva.TryGoOutside().Succeeded, Is.True);
            yield return Frames(3);
            Assert.That(harness.Player.IsOnRoute, Is.True);
            Assert.That(harness.InnerGate.IsOpen, Is.False);

            harness.Runtime.RestartMission();
            yield return Frames(3);

            Assert.That(harness.Session, Is.Not.SameAs(old));
            Assert.That(harness.Session.State.Phase, Is.EqualTo(GamePhase.Planning));
            Assert.That(harness.Session.Eva.Airlock, Is.EqualTo(AirlockState.InnerOpen));
            Assert.That(harness.Player.IsOnRoute, Is.False);
            Assert.That(harness.Player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(Vector3.Distance(harness.Player.transform.position, RepairLoopHarness.SpawnPosition), Is.LessThan(0.2f));
            Assert.That(harness.InnerGate.IsOpen, Is.True);
        }
    }
}
