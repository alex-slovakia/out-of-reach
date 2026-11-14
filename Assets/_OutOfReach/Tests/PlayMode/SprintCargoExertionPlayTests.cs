using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    public sealed class SprintCargoExertionPlayTests
    {
        private RepairLoopHarness harness;
        private SprintBridge input;

        private sealed class SprintBridge : IPlayerInputSource, ISprintInputSource
        {
            private readonly ScriptedInput source;
            public SprintBridge(ScriptedInput source) { this.source = source; }
            public Vector2 Move => source.Move;
            public Vector2 LookDegrees => source.LookDegrees;
            public bool InteractPressedThisFrame => source.InteractPressedThisFrame;
            public bool PausePressedThisFrame => source.PausePressedThisFrame;
            public bool SprintHeld { get; set; }
        }

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = 0.02f;
            harness = RepairLoopHarness.Build(false, true);
            harness.Runtime.DriveFromUpdate = false;
            input = new SprintBridge(harness.Input);
            harness.Player.InputSource = input;
            // The composition integration hook; no Core contract or fixture is changed.
            harness.Runtime.SessionStarted += OnSessionStarted;
        }

        private void OnSessionStarted(MissionSession session) { harness.Player.ResetExertionForNewSession(); }

        [TearDown]
        public void TearDown()
        {
            harness.Runtime.SessionStarted -= OnSessionStarted;
            harness.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator RealCratePickup_BlocksSprintCostsExertion_AndNeverSpendsShipBattery()
        {
            yield return Frames(3);
            Vector3 target = RepairLoopHarness.PalletPosition(AuroraMission.BatteryModule);
            harness.PlacePlayerFacing(target.x, 6.5f, target);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.CargoSupply[AuroraMission.BatteryModule]));
            harness.Input.PressInteract();
            yield return Frames(3);
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.BatteryModule));
            float carry = harness.Player.SpeedMultiplier;
            Assert.That(carry, Is.EqualTo(harness.Runtime.Cargo.CarrySpeedFactor));
            Assert.That(carry, Is.LessThan(1f));
            harness.PlacePlayerFacing(-5f, 6.5f, new Vector3(6f, 0f, 6.5f));
            yield return Frames(3);
            float start = harness.Player.transform.position.x;
            double battery = harness.Session.Resources.Snapshot.BatteryKWh;
            input.SprintHeld = true;
            harness.Input.Move = Vector2.up;
            yield return Frames(25);
            harness.Input.Move = Vector2.zero;
            Assert.That(harness.Player.transform.position.x - start,
                Is.EqualTo(harness.Player.WalkSpeedMetresPerSecond * carry * 0.5f).Within(0.05f));
            Assert.That(harness.Player.IsSprinting, Is.False);
            Assert.That(harness.Player.ExertionRemaining01, Is.LessThan(1f));
            Assert.That(harness.Session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery));
            float remaining = harness.Player.ExertionRemaining01;
            harness.Runtime.RestartMission();
            Assert.That(remaining, Is.LessThan(1f));
            Assert.That(harness.Player.ExertionRemaining01, Is.EqualTo(1f));
            yield return Frames(3);
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.Null);
            Assert.That(harness.Player.IsSprinting, Is.False);
            harness.Input.Move = Vector2.up;
            yield return Frames(3);
            Assert.That(harness.Player.IsSprinting, Is.False, "in-place restart cannot inherit held sprint");
        }

        [UnityTest]
        public IEnumerator RealPauseAndImpactCinematic_StopSprintAndFreezeStamina()
        {
            yield return Frames(3);
            harness.PlacePlayerFacing(-5f, 6.5f, new Vector3(6f, 0f, 6.5f));
            yield return Frames(3);
            input.SprintHeld = true;
            harness.Input.Move = Vector2.up;
            yield return Frames(10);
            Assert.That(harness.Player.IsSprinting, Is.True);
            harness.Input.PressPause();
            yield return Frames(2);
            Assert.That(harness.Runtime.IsPaused, Is.True);
            float remaining = harness.Player.ExertionRemaining01;
            Vector3 stopped = harness.Player.transform.position;
            yield return Frames(10);
            Assert.That(harness.Player.ExertionRemaining01, Is.EqualTo(remaining));
            Assert.That(harness.Player.transform.position, Is.EqualTo(stopped));
            Assert.That(harness.Player.IsSprinting, Is.False);
            harness.Input.PressPause();
            yield return Frames(3);
            Assert.That(harness.Runtime.IsPaused, Is.False);
            Assert.That(harness.Player.IsSprinting, Is.False);

            harness.Input.Move = Vector2.zero;
            input.SprintHeld = false;
            harness.Launch();
            harness.Session.Events.CinematicRequested += IgnoreCinematic;
            Assert.That(harness.Session.Director.TryCompleteCockpitCheck().Succeeded, Is.True);
            Assert.That(harness.Session.Cinematics.IsWaiting, Is.True);
            yield return Frames(2);
            remaining = harness.Player.ExertionRemaining01;
            stopped = harness.Player.transform.position;
            input.SprintHeld = true;
            harness.Input.Move = Vector2.up;
            yield return Frames(10);
            Assert.That(harness.Player.ControlAllowed, Is.False);
            Assert.That(harness.Player.IsSprinting, Is.False);
            Assert.That(harness.Player.ExertionRemaining01, Is.EqualTo(remaining));
            Assert.That(harness.Player.transform.position, Is.EqualTo(stopped));
        }

        private static void IgnoreCinematic(CinematicRequested requested) { }
    }
}
