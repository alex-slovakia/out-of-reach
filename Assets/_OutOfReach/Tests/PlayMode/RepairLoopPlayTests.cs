using System.Collections;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    public sealed class RepairLoopPlayTests
    {
        private const float FrameSeconds = 0.02f;

        private RepairLoopHarness harness;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build();
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

        private float DistanceToPanelFace(float playerX)
        {
            // The power panel is 0.3 m thick and centred at x = -4, so its near face is at x = -3.85.
            return playerX - (RepairLoopHarness.PowerPanelPosition.x + 0.15f);
        }

        [UnityTest]
        public IEnumerator Player_CannotMoveBeforeLaunch_ThenWalksAtTheConfiguredSpeed_AndIsStoppedByGeometry()
        {
            harness.Input.Move = new Vector2(0f, 1f);
            yield return Frames(10);
            Assert.That(harness.Player.transform.position.z, Is.EqualTo(0f).Within(0.001f), "no walking during Planning");
            Assert.That(harness.Player.ControlAllowed, Is.False);

            harness.Launch();
            harness.Input.Move = Vector2.zero;
            yield return Frames(5);
            float startZ = harness.Player.transform.position.z;

            harness.Input.Move = new Vector2(0f, 1f);
            yield return Frames(25);
            float travelled = harness.Player.transform.position.z - startZ;
            Assert.That(travelled, Is.EqualTo(harness.Player.WalkSpeedMetresPerSecond * 25 * FrameSeconds).Within(0.1f), "2.5 m/s for 0.5 s");

            yield return Frames(150);
            float z = harness.Player.transform.position.z;
            Assert.That(z, Is.LessThan(RepairLoopHarness.ConsolePosition.z - 0.15f - 0.25f), "the console block stops the capsule");
            Assert.That(z, Is.GreaterThan(2.2f), "but only when the player actually reaches it");
            Assert.That(harness.Player.transform.position.y, Is.EqualTo(0f).Within(0.1f), "standing on the floor");
        }

        [UnityTest]
        public IEnumerator Look_TurnsTheBody_AndPitchIsBounded()
        {
            harness.Launch();
            yield return Frames(2);

            harness.Input.QueueLook(45f, 0f);
            yield return Frames(2);
            Assert.That(harness.Player.transform.eulerAngles.y, Is.EqualTo(45f).Within(0.01f));

            harness.Input.QueueLook(0f, 200f);
            yield return Frames(2);
            Assert.That(harness.Player.PitchDegrees, Is.EqualTo(harness.Player.MaxPitchDegrees));
            Assert.That(Mathf.DeltaAngle(0f, harness.ViewPivot.localEulerAngles.x), Is.EqualTo(-harness.Player.MaxPitchDegrees).Within(0.01f), "looking up");

            harness.Input.QueueLook(0f, -400f);
            yield return Frames(2);
            Assert.That(harness.Player.PitchDegrees, Is.EqualTo(-harness.Player.MaxPitchDegrees));
        }

        [UnityTest]
        public IEnumerator Focus_NeedsReach_AndAClearLineOfSight()
        {
            harness.ReachEmergency();
            Vector3 panel = RepairLoopHarness.PowerPanelPosition;

            harness.PlacePlayerFacing(panel.x + 0.15f + 3f, 0f, panel);
            yield return Frames(3);
            Assert.That(DistanceToPanelFace(harness.Player.transform.position.x), Is.EqualTo(3f).Within(0.05f));
            Assert.That(harness.Resolver.CurrentTarget, Is.Null, "3 m is beyond the 2 m reach");
            Assert.That(harness.Resolver.CurrentPrompt, Is.Empty);
            Assert.That(harness.Resolver.TryInteract().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(harness.Session.Resources.Snapshot.SparePartsCount, Is.EqualTo(7));

            harness.PlacePlayerFacing(panel.x + 0.15f + 1.5f, 0f, panel);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.PowerPanel));
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Repair power distribution: 2 spare parts."));
            Assert.That(harness.Resolver.CurrentCanInteract, Is.True);

            GameObject wall = harness.AddWall(new Vector3(panel.x + 0.9f, 1.25f, 0f), new Vector3(0.2f, 2.5f, 2f));
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.Null, "a wall between the player and the panel blocks the interaction");
            Assert.That(harness.Resolver.TryInteract().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(harness.Session.Systems.Power.IsOperational, Is.False);
            Assert.That(harness.Session.Resources.Snapshot.SparePartsCount, Is.EqualTo(7));

            Object.Destroy(wall);
            yield return Frames(3);
            Physics.SyncTransforms();
            yield return Frames(2);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.PowerPanel), "the focus returns when the wall is gone");
        }

        [UnityTest]
        public IEnumerator InteractPress_RepairsOnce_SpendsTheExactCost_AndARepeatIsRejected()
        {
            harness.ReachEmergency();
            Vector3 panel = RepairLoopHarness.PowerPanelPosition;
            harness.PlacePlayerFacing(panel.x + 0.15f + 1.5f, 0f, panel);
            yield return Frames(3);
            int systemEventsBefore = harness.Listener.SystemConditions.Count;

            harness.Input.PressInteract();
            yield return Frames(2);

            Assert.That(harness.Player.LastInteraction.HasValue, Is.True);
            Assert.That(harness.Player.LastInteraction.Value.Succeeded, Is.True, harness.Player.LastInteraction.Value.Explanation);
            Assert.That(harness.Session.Resources.Snapshot.SparePartsCount, Is.EqualTo(5), "exactly two parts");
            Assert.That(harness.Session.Systems.Power.IsOperational, Is.True);
            Assert.That(harness.Session.State.Phase, Is.EqualTo(GamePhase.Repair));
            Assert.That(harness.Listener.SystemConditions.Count, Is.EqualTo(systemEventsBefore + 1), "one success event");
            Assert.That(harness.Listener.SystemConditions.Last().SystemId, Is.EqualTo(ShipSystemId.Power));
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Power distribution: repaired"));

            harness.Input.PressInteract();
            yield return Frames(2);

            Assert.That(harness.Player.LastInteraction.Value.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
            Assert.That(harness.Session.Resources.Snapshot.SparePartsCount, Is.EqualTo(5), "never charged twice");
            Assert.That(harness.Listener.SystemConditions.Count, Is.EqualTo(systemEventsBefore + 1), "no duplicate success event");
        }

        [UnityTest]
        public IEnumerator Pause_FreezesThePlayerAndTheClock_AndReleasesTheCursor()
        {
            harness.ReachEmergency();
            yield return Frames(5);
            Assert.That(harness.Session.State.ElapsedSimulationSeconds, Is.GreaterThan(0d), "the runtime drives the clock from Update");
            Assert.That(harness.Player.WantsCursorCaptured, Is.True);

            harness.Input.PressPause();
            yield return Frames(2);
            Assert.That(harness.Runtime.IsPaused, Is.True);
            Assert.That(harness.Player.ControlAllowed, Is.False);
            Assert.That(harness.Player.WantsCursorCaptured, Is.False, "the cursor is released while paused");

            double elapsed = harness.Session.State.ElapsedSimulationSeconds;
            double oxygen = harness.Session.Resources.Snapshot.OxygenKg;
            Vector3 position = harness.Player.transform.position;
            harness.Input.Move = new Vector2(0f, 1f);
            harness.Input.QueueLook(30f, 0f);
            yield return Frames(20);
            Assert.That(harness.Session.State.ElapsedSimulationSeconds, Is.EqualTo(elapsed));
            Assert.That(harness.Session.Resources.Snapshot.OxygenKg, Is.EqualTo(oxygen));
            Assert.That(Vector3.Distance(harness.Player.transform.position, position), Is.LessThan(0.001f));
            Assert.That(harness.Resolver.CurrentTarget, Is.Null);

            harness.Input.Move = Vector2.zero;
            harness.Input.PressPause();
            yield return Frames(5);
            Assert.That(harness.Runtime.IsPaused, Is.False);
            Assert.That(harness.Session.State.ElapsedSimulationSeconds, Is.GreaterThan(elapsed), "no stuck pause");
            Assert.That(harness.Player.WantsCursorCaptured, Is.True);
        }

        [UnityTest]
        public IEnumerator ImpactCinematic_LocksControl_AndTheFallbackAlwaysReturnsIt()
        {
            harness.Runtime.DriveFromUpdate = false;
            harness.Launch();
            // A presentation listener exists, so Core waits for its acknowledgement (or the fallback timeout).
            harness.Session.Events.CinematicRequested += IgnoreCinematic;
            Vector3 console = RepairLoopHarness.ConsolePosition;
            harness.PlacePlayerFacing(0f, console.z - 0.15f - 1.5f, console);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Run cockpit systems check"));

            harness.Input.PressInteract();
            yield return Frames(2);
            Assert.That(harness.Session.Director.AccidentTriggered, Is.True);
            Assert.That(harness.Session.Cinematics.IsWaiting, Is.True);
            Assert.That(harness.Player.ControlAllowed, Is.False, "input is locked during the impact");

            Vector3 position = harness.Player.transform.position;
            harness.Input.Move = new Vector2(0f, -1f);
            harness.Input.PressInteract();
            yield return Frames(10);
            Assert.That(Vector3.Distance(harness.Player.transform.position, position), Is.LessThan(0.001f));
            Assert.That(harness.Session.State.Phase, Is.EqualTo(GamePhase.Intro));

            harness.Input.Move = Vector2.zero;
            Assert.That(harness.Runtime.Step(1d).Succeeded, Is.True);
            Assert.That(harness.Session.Cinematics.IsWaiting, Is.True, "one clamped frame is far from the fallback timeout");
            Assert.That(harness.Session.Tick(harness.Session.Definition.Scenario.ImpactCinematicTimeoutRealSeconds).Succeeded, Is.True);
            yield return Frames(2);

            Assert.That(harness.Session.State.Phase, Is.EqualTo(GamePhase.Emergency));
            Assert.That(harness.Player.ControlAllowed, Is.True, "a missing acknowledgement can never strand the player");
            Assert.That(harness.Session.Systems.Power.Condition01, Is.EqualTo(0.2d), "damage applied exactly once");
        }

        [UnityTest]
        public IEnumerator DisableEnable_AndRestart_NeverDuplicateSubscriptions_AndRebindTheScene()
        {
            harness.Launch();
            yield return Frames(2);
            int subscribers = harness.Session.Events.TotalSubscriberCount;
            Assert.That(harness.Listener.IsBound, Is.True);

            for (int i = 0; i < 3; i++)
            {
                harness.Listener.enabled = false;
                harness.Listener.enabled = true;
                harness.Runtime.enabled = false;
                harness.Runtime.enabled = true;
                harness.Player.enabled = false;
                harness.Player.enabled = true;
                yield return null;
            }

            Assert.That(harness.Session.Events.TotalSubscriberCount, Is.EqualTo(subscribers), "re-enabling adds no listeners");

            harness.Input.PressPause();
            yield return Frames(2);
            Assert.That(harness.Runtime.IsPaused, Is.True, "exactly one pause toggle per press after re-enabling");
            harness.Input.PressPause();
            yield return Frames(2);
            Assert.That(harness.Runtime.IsPaused, Is.False);

            MissionSession old = harness.Session;
            harness.PlacePlayerFacing(2f, 2f, Vector3.zero);
            int phaseEventsBefore = harness.Listener.PhaseEvents;

            harness.Runtime.RestartMission();
            yield return Frames(2);

            MissionSession fresh = harness.Session;
            Assert.That(fresh, Is.Not.SameAs(old));
            Assert.That(old.IsDisposed, Is.True);
            Assert.That(old.Events.TotalSubscriberCount, Is.EqualTo(0));
            Assert.That(fresh.State.Phase, Is.EqualTo(GamePhase.Planning));
            Assert.That(fresh.Events.TotalSubscriberCount, Is.EqualTo(subscribers), "the same listeners, bound once");
            Assert.That(harness.Player.transform.position.x, Is.EqualTo(RepairLoopHarness.SpawnPosition.x).Within(0.01f), "back at the spawn");
            Assert.That(harness.Player.transform.position.z, Is.EqualTo(RepairLoopHarness.SpawnPosition.z).Within(0.01f));
            Assert.That(fresh.Interactions.TryGet(InteractionIds.RepairPower, out IInteractable power), Is.True);
            Assert.That(harness.PowerPanel.Interactable, Is.SameAs(power), "targets are rebound to the new session");

            fresh.Planner.TryLaunch();
            yield return null;
            Assert.That(harness.Listener.PhaseEvents, Is.EqualTo(phaseEventsBefore + 1), "one event from the new session, none duplicated");
            Assert.That(old.Planner.TryLaunch().Failure, Is.EqualTo(CommandFailure.WrongPhase), "the old session is closed");
        }

        [Test]
        public void DefaultBindings_CoverKeyboardMouseAndGamepad()
        {
            using (var actions = new PlayerInputActions())
            {
                string[] paths = actions.Map.bindings.Select(binding => binding.path).ToArray();

                Assert.That(paths, Does.Contain("<Keyboard>/w"));
                Assert.That(paths, Does.Contain("<Keyboard>/s"));
                Assert.That(paths, Does.Contain("<Keyboard>/a"));
                Assert.That(paths, Does.Contain("<Keyboard>/d"));
                Assert.That(paths, Does.Contain("<Mouse>/delta"));
                Assert.That(paths, Does.Contain("<Keyboard>/e"));
                Assert.That(paths, Does.Contain("<Keyboard>/escape"));
                Assert.That(paths, Does.Contain("<Gamepad>/leftStick"));
                Assert.That(paths, Does.Contain("<Gamepad>/rightStick"));
                Assert.That(paths, Does.Contain("<Gamepad>/buttonSouth"));
                Assert.That(actions.Map.FindAction("Interact"), Is.Not.Null);
                Assert.That(actions.Map.asset, Is.Null, "defined in code: the shared project input asset is untouched");
            }
        }

        private static void IgnoreCinematic(CinematicRequested request)
        {
        }
    }
}
