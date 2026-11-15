using System.Collections;
using NUnit.Framework;
using OutOfReach.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// MOVE-MECH-01 (corrected movement contract #7 6893683804 + 6893693954), Player side: the faster push tuning,
    /// the shared outward-cone direction, A/D view lean while floating (orientation only, never motion), the
    /// read-only foot support used to choose legs over hands, and the remaining push cooldown used to queue a press.
    /// The composition routing (mouse grip/push selection) is verified in the actual scene, not here.
    /// </summary>
    public sealed class MoveMech01PlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const float Eye = RepairLoopHarness.EyeHeightMetres;

        private RepairLoopHarness harness;
        private bool gateOpen;
        private Collider floor;
        private Collider back;

        private FirstPersonController Player => harness.Player;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build();
            harness.Runtime.DriveFromUpdate = false;
            harness.Runtime.enabled = false;
            gateOpen = true;
            harness.Player.ControlGate = () => gateOpen;
            // Closed cabin, interior x -2..2, y 50..53, z 0..6.
            floor = harness.AddWall(new Vector3(0f, 49.9f, 3f), new Vector3(4.4f, 0.2f, 6.4f)).GetComponent<Collider>();
            harness.AddWall(new Vector3(0f, 53.1f, 3f), new Vector3(4.4f, 0.2f, 6.4f));
            harness.AddWall(new Vector3(-2.1f, 51.5f, 3f), new Vector3(0.2f, 3.4f, 6.4f));
            harness.AddWall(new Vector3(2.1f, 51.5f, 3f), new Vector3(0.2f, 3.4f, 6.4f));
            back = harness.AddWall(new Vector3(0f, 51.5f, -0.1f), new Vector3(4.4f, 3.4f, 0.2f)).GetComponent<Collider>();
            harness.AddWall(new Vector3(0f, 51.5f, 6.1f), new Vector3(4.4f, 3.4f, 0.2f));
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

        private Vector3 Position => Player.transform.position;

        private void FloatAt(Vector3 feet)
        {
            Player.Teleport(feet, 0f);
            Physics.SyncTransforms();
            Assert.That(Player.EnterFloat(), Is.EqualTo(FloatActionResult.Done));
        }

        [Test]
        public void Tuning_IsTheFasterDisclosedResponse_WithNoHiddenDrag()
        {
            var tuning = new MicrogravityLocomotion();
            Assert.That(tuning.MaxPushSpeed, Is.EqualTo(1.5f));
            Assert.That(tuning.MaxSpeed, Is.EqualTo(2.2f));
            Assert.That(tuning.PushCooldownSeconds, Is.EqualTo(0.25f));
            Assert.That(tuning.DragPerSecond, Is.Zero, "still no hidden force");
            Assert.That(tuning.MaxLeanDegrees, Is.EqualTo(30f));
            Assert.That(tuning.Push(Vector3.zero, Vector3.forward, 0.55f).magnitude, Is.EqualTo(0.825f).Within(1e-5f),
                "one medium hand push");
            Assert.That(tuning.PushPair(Vector3.zero, Vector3.forward, 0.55f, Vector3.forward, 0.55f).magnitude,
                Is.EqualTo(1.5f).Within(1e-5f), "two hands make one full push, never more");
        }

        [Test]
        public void OutwardDirection_KeepsAnAwayIntent_AndLeavesAlongTheSurfaceOtherwise()
        {
            Vector3 normal = Vector3.up;
            Assert.That(MicrogravityLocomotion.TryOutwardDirection(normal, new Vector3(0f, 1f, 1f), 0.2f, out Vector3 away), Is.True);
            Assert.That(Vector3.Distance(away, new Vector3(0f, 1f, 1f).normalized), Is.LessThan(1e-5f), "already away: kept");

            Assert.That(MicrogravityLocomotion.TryOutwardDirection(normal, Vector3.forward, 0.2f, out Vector3 along), Is.True);
            Assert.That(Vector3.Dot(along, normal), Is.EqualTo(0.25f).Within(1e-4f), "a level intent leaves at the cone");
            Assert.That(along.z, Is.GreaterThan(0.96f), "mostly along the intent");

            Assert.That(MicrogravityLocomotion.TryOutwardDirection(normal, Vector3.down, 0.2f, out Vector3 straight), Is.True);
            Assert.That(Vector3.Distance(straight, normal), Is.LessThan(1e-5f), "straight into the surface: straight off it");

            Assert.That(MicrogravityLocomotion.TryOutwardDirection(normal, Vector3.zero, 0.2f, out _), Is.False);
            Assert.That(MicrogravityLocomotion.TryOutwardDirection(normal, new Vector3(float.NaN, 0f, 1f), 0.2f, out _), Is.False);
            for (int i = 0; i < 90; i++)
            {
                Vector3 intent = Quaternion.AngleAxis(i * 4f, Vector3.right) * Vector3.forward;
                Assert.That(MicrogravityLocomotion.TryOutwardDirection(normal, intent, 0.2f, out Vector3 d), Is.True);
                Assert.That(Vector3.Dot(d, normal), Is.GreaterThanOrEqualTo(0.2f), "always a valid push for the Player");
                Assert.That(d.magnitude, Is.EqualTo(1f).Within(1e-4f));
            }
        }

        [Test]
        public void StepLean_HoldsToTheLimit_ReturnsLevel_AndIgnoresBadInput()
        {
            var tuning = new MicrogravityLocomotion();
            float lean = 0f;
            for (int i = 0; i < 100; i++) lean = tuning.StepLean(lean, -1f, 0.02f);
            Assert.That(lean, Is.EqualTo(30f).Within(1e-4f), "A leans left (positive) to the limit");
            for (int i = 0; i < 100; i++) lean = tuning.StepLean(lean, 0f, 0.02f);
            Assert.That(lean, Is.Zero, "released: back to level");
            for (int i = 0; i < 100; i++) lean = tuning.StepLean(lean, 1f, 0.02f);
            Assert.That(lean, Is.EqualTo(-30f).Within(1e-4f), "D leans right");
            Assert.That(tuning.StepLean(float.NaN, 1f, 0.02f), Is.Zero);
            Assert.That(tuning.StepLean(10f, 1f, float.NaN), Is.EqualTo(10f));
            Assert.That(tuning.StepLean(10f, float.NaN, 0.02f), Is.LessThan(10f), "NaN input counts as released");
        }

        [UnityTest]
        public IEnumerator AandD_WhileFloating_OnlyLeanTheView_NeverMoveTheBody()
        {
            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            Vector3 start = Position;
            harness.Input.Move = new Vector2(-1f, 0f);
            yield return Frames(40);
            Assert.That(Player.LeanDegrees, Is.EqualTo(Player.Microgravity.MaxLeanDegrees).Within(1e-3f), "A: full left lean");
            Assert.That(Player.ViewPivot.localEulerAngles.z, Is.EqualTo(Player.Microgravity.MaxLeanDegrees).Within(0.05f));
            Assert.That(Vector3.Distance(Position, start), Is.LessThan(1e-4f), "no thrust");
            Assert.That(Player.FloatVelocity, Is.EqualTo(Vector3.zero), "no hidden force");

            harness.Input.Move = Vector2.zero;
            yield return Frames(40);
            Assert.That(Player.LeanDegrees, Is.Zero, "released: level again");
            Assert.That(Vector3.Distance(Position, start), Is.LessThan(1e-4f));
        }

        [UnityTest]
        public IEnumerator Walking_NeverLeans_AndLeavingFloatReturnsLevel()
        {
            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            harness.Input.Move = new Vector2(1f, 0f);
            yield return Frames(20);
            Assert.That(Player.LeanDegrees, Is.LessThan(-1f), "D leans right while floating");

            Player.ExitFloat();
            yield return Frames(40);
            Assert.That(Player.LeanDegrees, Is.Zero, "walking returns level even with D held");
            harness.Input.Move = Vector2.zero;

            Player.Teleport(new Vector3(0f, 50.2f, 1.5f), 0f);
            Assert.That(Player.LeanDegrees, Is.Zero);
            Assert.That(Quaternion.Angle(Player.ViewPivot.localRotation, Quaternion.identity), Is.LessThan(1e-3f), "teleport: level view");
        }

        [UnityTest]
        public IEnumerator FootSupport_IsReadOnly_AndOnlyTrueOnTheDeckWhileFloating()
        {
            Player.Teleport(new Vector3(0f, 50.05f, 1.5f), 0f);
            yield return Frames(10);
            Assert.That(Player.TryGetFootSupport(out _), Is.False, "walking: not a float query");

            Assert.That(Player.EnterFloatFromFootSupport(), Is.EqualTo(FloatActionResult.Done));
            Assert.That(Player.TryGetFootSupport(out Vector3 deck), Is.True, "standing on the deck");
            Assert.That(Vector3.Dot(deck, Vector3.up), Is.GreaterThan(0.99f));
            Vector3 still = Position;
            Assert.That(Player.TryGetFootSupport(out _), Is.True);
            Assert.That(Position, Is.EqualTo(still), "a query never moves the body");

            Assert.That(MicrogravityLocomotion.TryOutwardDirection(deck, Vector3.forward, Player.Microgravity.MinimumAwayDot, out Vector3 legs), Is.True);
            Assert.That(Player.TryPushFromFootContact(legs, 1f), Is.EqualTo(FloatActionResult.Done), "W + Space from the deck");
            Assert.That(Player.FloatVelocity.magnitude, Is.EqualTo(Player.Microgravity.MaxPushSpeed).Within(1e-4f));
            Assert.That(Player.FloatVelocity.z, Is.GreaterThan(1.4f), "mostly forward");
            yield return Frames(25);
            Assert.That(Player.TryGetFootSupport(out _), Is.False, "left the deck: hands only now");
        }

        [UnityTest]
        public IEnumerator PushCooldownRemaining_CountsDownAndIsZeroWhenAPushMayRun()
        {
            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            Assert.That(Player.PushCooldownRemaining, Is.Zero);
            Vector3 point = new Vector3(Position.x, Position.y + Eye, 0f);
            Assert.That(Player.TryPushOff(back, point, Vector3.forward, 0.55f), Is.EqualTo(FloatActionResult.Done));
            Assert.That(Player.PushCooldownRemaining, Is.EqualTo(Player.Microgravity.PushCooldownSeconds).Within(1e-4f));
            yield return Frames(5);
            Assert.That(Player.PushCooldownRemaining, Is.GreaterThan(0f).And.LessThan(Player.Microgravity.PushCooldownSeconds));
            yield return Frames(10);
            Assert.That(Player.PushCooldownRemaining, Is.Zero);
            Player.ExitFloat();
            Assert.That(Player.PushCooldownRemaining, Is.Zero, "walking: no cooldown to wait for");
            Assert.That(floor, Is.Not.Null);
        }
    }
}
