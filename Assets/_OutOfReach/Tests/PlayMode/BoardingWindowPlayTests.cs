using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// C05-BOARDING-01 in the grey-box harness (physical preflight with the boarding switch on): the expiring loading
    /// deadline seals the cargo without a launch or a teleport, and the console (the transport seat in the real scene)
    /// launches through a real E press. The legacy flow (switch off) is covered unchanged by PreflightPlayTests.
    /// </summary>
    public sealed class BoardingWindowPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const float WalkwayZ = 6.5f;

        private RepairLoopHarness harness;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build(preflight: true, boarding: true);
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

        private IEnumerator Face(string equipmentId, bool pallet)
        {
            Vector3 target = pallet ? RepairLoopHarness.PalletPosition(equipmentId) : RepairLoopHarness.BayStackPosition(equipmentId);
            harness.PlacePlayerFacing(target.x, WalkwayZ, target);
            yield return Frames(3);
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(pallet ? harness.CargoSupply[equipmentId] : harness.CargoBay[equipmentId]));
        }

        [UnityTest]
        public IEnumerator Expiry_OnThePad_SealsTheLastValidLoadout_WithoutATeleport_AndTheSeatLaunchesIt()
        {
            MissionSession session = harness.Session;
            harness.Runtime.BeginTimedPlanning();
            Assert.That(harness.Runtime.BoardAfterLoadingDeadline, Is.True);

            // The solar wing is taken out of the bay and kept in hands: the draft cannot power the instrument.
            yield return Face(AuroraMission.SolarWing, false);
            yield return PressInteract();
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.SolarWing));
            Assert.That(harness.Runtime.Planner.Draft.Validate().Succeeded, Is.False);
            Vector3 atExpiry = harness.Player.transform.position;

            Assert.That(harness.Runtime.Step(61d).Succeeded, Is.True);
            yield return Frames(3);

            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning), "no silent launch");
            Assert.That(harness.Runtime.AwaitingBoarding, Is.True);
            Assert.That(harness.Runtime.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.True);
            Assert.That(harness.Runtime.Planner.Draft.Validate().Succeeded, Is.True, "the last valid loadout is sealed");
            Assert.That(Vector3.Distance(harness.Player.transform.position, atExpiry), Is.LessThan(0.05f), "no teleport at expiry");
            Assert.That(harness.Player.ControlAllowed, Is.True);
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.Null, "the crate in hands stays behind");
            Assert.That(harness.Player.SpeedMultiplier, Is.EqualTo(1f));

            // Cargo is closed through the real resolver and E press.
            yield return Face(AuroraMission.OxygenTank, true);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo(PlanningDeadline.LoadingClosedMessage));
            Assert.That(harness.Resolver.CurrentCanInteract, Is.False);
            yield return PressInteract();
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.Null);

            // Still free to walk towards the seat.
            harness.PlacePlayerFacing(-5f, WalkwayZ, new Vector3(6f, 0f, WalkwayZ));
            yield return Frames(2);
            float startX = harness.Player.transform.position.x;
            harness.Input.Move = new Vector2(0f, 1f);
            yield return Frames(25);
            harness.Input.Move = Vector2.zero;
            Assert.That(harness.Player.transform.position.x - startX, Is.GreaterThan(0.5f), "the player walks in the window");

            // The seat: E on the console launches the sealed manifest once, then the usual move to the cockpit.
            Vector3 console = RepairLoopHarness.ConsolePosition;
            harness.PlacePlayerFacing(0f, console.z - 0.15f - 1.5f, console);
            yield return Frames(3);
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.Console));
            Assert.That(harness.Resolver.CurrentPrompt, Does.StartWith("Secure your seat and launch: "));
            Assert.That(harness.Resolver.CurrentCanInteract, Is.True);
            yield return PressInteract();

            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
            Assert.That(harness.Runtime.AwaitingBoarding, Is.False);
            Assert.That(session.Configuration.AvailablePowerWatts, Is.EqualTo(1900d), "the wing of the last valid loadout is aboard");
            Assert.That(harness.Player.transform.position.z, Is.EqualTo(RepairLoopHarness.SpawnPosition.z).Within(0.05f), "moved to the cockpit at launch");
            Assert.That(session.Planner.TryLaunch().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "one launch");
        }

        [UnityTest]
        public IEnumerator Pause_KeepsTheWindow_AndRestartReturnsToAnOpenPad()
        {
            harness.Runtime.BeginTimedPlanning();
            Assert.That(harness.Runtime.Step(61d).Succeeded, Is.True);
            yield return Frames(3);
            Assert.That(harness.Runtime.AwaitingBoarding, Is.True);

            harness.Runtime.SetPaused(true);
            for (int i = 0; i < 10; i++)
            {
                harness.Runtime.Step(10d);
            }

            yield return Frames(3);
            Assert.That(harness.Player.ControlAllowed, Is.False, "pause locks control");
            Assert.That(harness.Runtime.AwaitingBoarding, Is.True);
            harness.Runtime.SetPaused(false);
            harness.Runtime.Step(1d);
            yield return Frames(3);
            Assert.That(harness.Session.State.Phase, Is.EqualTo(GamePhase.Planning), "resuming launches nothing");
            Assert.That(harness.Runtime.AwaitingBoarding, Is.True);
            Assert.That(harness.Player.ControlAllowed, Is.True);

            harness.Runtime.RestartMission();
            yield return Frames(3);

            Assert.That(harness.Session.State.Phase, Is.EqualTo(GamePhase.Planning));
            Assert.That(harness.Session.BoardingAfterDeadlineEnabled, Is.True, "the switch survives the restart");
            Assert.That(harness.Runtime.AwaitingBoarding, Is.False);
            Assert.That(harness.Session.Planner.IsSealed, Is.False);
            Assert.That(harness.Runtime.PlanningDeadline.IsEnabled, Is.False, "practice until timed planning starts again");
            Assert.That(harness.Player.transform.position.z, Is.EqualTo(RepairLoopHarness.PreflightSpawnPosition.z).Within(0.05f));
            yield return Face(AuroraMission.FoodLocker, true);
            Assert.That(harness.Resolver.CurrentCanInteract, Is.True, "cargo is open again");
        }
    }
}
