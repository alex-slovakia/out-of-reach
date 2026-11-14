using System.Collections;
using NUnit.Framework;
using OutOfReach.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// MICROGRAVITY-PROTOTYPE-01 (Codex 6881635025, refined in 6881718301): the optional float mode of the one player
    /// controller, in a closed test cabin built 50 m above the harness. No thrust and no hidden force: motion only from
    /// verified pushes, registered handholds and inelastic contacts; collision sweeps prevent tunneling; a drift is
    /// the same at 30 and 240 fps; pause and cinematics keep the drift; walking returns on exit.
    /// </summary>
    public sealed class MicrogravityLocomotionPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const float Eye = RepairLoopHarness.EyeHeightMetres;

        private RepairLoopHarness harness;
        private bool gateOpen;
        private Collider floor;
        private Collider ceiling;
        private Collider back;
        private Collider front;
        private Collider left;
        private Collider dividerLeft;
        private Collider handhold;

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
            BuildCabin();
        }

        [TearDown]
        public void TearDown()
        {
            harness.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Closed cabin, interior x -2..2, y 50..53, z 0..6, split at z = 3 by a 0.1 m wall with a 0.9 m x 2.2 m
        /// doorway at x = 0; one registered handhold on the left wall at z = 1.5.
        /// </summary>
        private void BuildCabin()
        {
            floor = harness.AddWall(new Vector3(0f, 49.9f, 3f), new Vector3(4.4f, 0.2f, 6.4f)).GetComponent<Collider>();
            ceiling = harness.AddWall(new Vector3(0f, 53.1f, 3f), new Vector3(4.4f, 0.2f, 6.4f)).GetComponent<Collider>();
            left = harness.AddWall(new Vector3(-2.1f, 51.5f, 3f), new Vector3(0.2f, 3.4f, 6.4f)).GetComponent<Collider>();
            harness.AddWall(new Vector3(2.1f, 51.5f, 3f), new Vector3(0.2f, 3.4f, 6.4f));
            back = harness.AddWall(new Vector3(0f, 51.5f, -0.1f), new Vector3(4.4f, 3.4f, 0.2f)).GetComponent<Collider>();
            front = harness.AddWall(new Vector3(0f, 51.5f, 6.1f), new Vector3(4.4f, 3.4f, 0.2f)).GetComponent<Collider>();
            dividerLeft = harness.AddWall(new Vector3(-1.225f, 51.5f, 3f), new Vector3(1.55f, 3f, 0.1f)).GetComponent<Collider>();
            harness.AddWall(new Vector3(1.225f, 51.5f, 3f), new Vector3(1.55f, 3f, 0.1f));
            harness.AddWall(new Vector3(0f, 52.6f, 3f), new Vector3(0.9f, 0.8f, 0.1f));
            handhold = harness.AddWall(new Vector3(-1.95f, 51.6f, 1.5f), new Vector3(0.1f, 0.06f, 0.4f)).GetComponent<Collider>();
            Player.RegisterHandhold(handhold);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static void Done(FloatActionResult result)
        {
            Assert.That(result, Is.EqualTo(FloatActionResult.Done));
        }

        private void Place(Vector3 feet)
        {
            Player.Teleport(feet, 0f);
            Physics.SyncTransforms();
        }

        private void FloatAt(Vector3 feet)
        {
            Place(feet);
            Done(Player.EnterFloat());
        }

        private Vector3 Position => Player.transform.position;

        /// <summary>The point on the back wall straight behind the eye.</summary>
        private Vector3 BackPoint => new Vector3(Position.x, Position.y + Eye, 0f);

        [UnityTest]
        public IEnumerator FloatIsOffByDefault_NeedsASurfaceInReach_AndExitWalksAgain()
        {
            Assert.That(Player.IsFloating, Is.False);
            Place(new Vector3(0f, 50.2f, 1.5f));
            Assert.That(Player.EnterFloat(), Is.EqualTo(FloatActionResult.NoSurface), "no safe entry in mid-cabin");
            Assert.That(Player.IsFloating, Is.False);

            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            Assert.That(Player.IsFloating, Is.True);
            Vector3 still = Position;
            yield return Frames(25);
            Assert.That(Vector3.Distance(Position, still), Is.LessThan(1e-4f), "no gravity, no drift without a push");

            Player.ExitFloat();
            Assert.That(Player.IsFloating, Is.False);
            yield return Frames(50);
            Assert.That(Position.y, Is.EqualTo(50f).Within(0.05f), "gravity returns: on the floor again");
        }

        [UnityTest]
        public IEnumerator WasdIsNoThrust()
        {
            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            Vector3 start = Position;
            harness.Input.Move = new Vector2(0.7f, 1f);
            yield return Frames(50);
            harness.Input.Move = Vector2.zero;
            Assert.That(Vector3.Distance(Position, start), Is.LessThan(1e-4f));
            Assert.That(Player.FloatVelocity, Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator APush_NeedsAVerifiedSurfaceInReachAndInSight_PointingAway_WithACooldownAndACap()
        {
            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            Vector3 point = BackPoint;
            Assert.That(Player.TryPushOff(null, point, Vector3.forward), Is.EqualTo(FloatActionResult.NoSurface));
            Assert.That(Player.TryPushOff(back, point, Vector3.back), Is.EqualTo(FloatActionResult.IntoSurface));
            Assert.That(Player.TryPushOff(front, new Vector3(-1.3f, 50.2f + Eye, 6f), Vector3.back), Is.EqualTo(FloatActionResult.OutOfReach));
            Assert.That(Player.TryPushOff(back, point, Vector3.zero), Is.EqualTo(FloatActionResult.InvalidInput));
            Assert.That(Player.TryPushOff(back, new Vector3(float.NaN, 0f, 0f), Vector3.forward), Is.EqualTo(FloatActionResult.InvalidInput));

            Collider trigger = harness.AddWall(new Vector3(-1.3f, 50.2f + Eye, 0.2f), new Vector3(0.3f, 0.3f, 0.1f)).GetComponent<Collider>();
            trigger.isTrigger = true;
            Assert.That(Player.TryPushOff(trigger, new Vector3(-1.3f, 50.2f + Eye, 0.25f), Vector3.forward), Is.EqualTo(FloatActionResult.NoSurface), "a trigger is not solid");

            GameObject blocker = harness.AddWall(new Vector3(-1.7f, 50.2f + Eye, 0.6f), new Vector3(0.1f, 0.4f, 0.4f));
            Assert.That(Player.TryPushOff(left, new Vector3(-2f, 50.2f + Eye, 0.6f), Vector3.right), Is.EqualTo(FloatActionResult.Occluded), "line of sight");
            blocker.SetActive(false);
            Physics.SyncTransforms();

            gateOpen = false;
            Assert.That(Player.TryPushOff(back, point, Vector3.forward), Is.EqualTo(FloatActionResult.ControlDenied));
            gateOpen = true;
            Assert.That(Player.FloatVelocity, Is.EqualTo(Vector3.zero), "refusals change nothing");

            Done(Player.TryPushOff(back, point, Vector3.forward));
            Assert.That(Vector3.Distance(Player.FloatVelocity, new Vector3(0f, 0f, Player.Microgravity.MaxPushSpeed)), Is.LessThan(1e-5f));
            Assert.That(Player.TryPushOff(back, point, Vector3.forward), Is.EqualTo(FloatActionResult.Cooldown), "a held button cannot keep accelerating");
            Assert.That(Player.FloatVelocity.magnitude, Is.EqualTo(Player.Microgravity.MaxPushSpeed).Within(1e-5f));

            Vector3 capped = Player.Microgravity.Push(new Vector3(0f, 0f, 1.2f), Vector3.forward, 1f);
            Assert.That(capped.magnitude, Is.EqualTo(Player.Microgravity.MaxSpeed).Within(1e-5f), "total speed is capped");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ADrift_KeepsItsSpeed_StopsInelasticallyAtAWall_AndNeverTunnelsWithLongFrames()
        {
            int contacts = 0;
            float impact = 0f;
            Player.FloatContact += (surface, speed) =>
            {
                contacts++;
                impact = speed;
            };

            foreach (float frame in new[] { 0.02f, 0.25f })
            {
                Time.captureDeltaTime = frame;
                contacts = 0;
                FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
                yield return null; // the new frame time applies from here
                Done(Player.TryPushOff(back, BackPoint, Vector3.forward));
                float speed = Player.FloatVelocity.magnitude;
                int frames = Mathf.CeilToInt(3f / frame);
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    Assert.That(Player.FloatVelocity.magnitude, Is.LessThanOrEqualTo(speed + 1e-5f), "never accelerates");
                    speed = Player.FloatVelocity.magnitude;
                    Assert.That(Position.z, Is.LessThan(2.66f), "never through the 0.1 m wall (frame " + frame + ")");
                }

                Assert.That(Position.z, Is.GreaterThan(2.55f), "stopped at the wall face");
                Assert.That(Player.FloatVelocity.z, Is.EqualTo(0f).Within(1e-4f), "inelastic stop");
                Assert.That(contacts, Is.GreaterThanOrEqualTo(1));
                Assert.That(impact, Is.EqualTo(Player.Microgravity.MaxPushSpeed).Within(0.05f));
            }
        }

        [UnityTest]
        public IEnumerator TheSameDrift_At30And240Fps_CoversTheSamePath()
        {
            var ends = new Vector3[2];
            int[] rates = { 30, 240 };
            for (int run = 0; run < 2; run++)
            {
                Time.captureDeltaTime = 1f / rates[run];
                FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
                yield return null; // the new frame time applies from here; floating still, nothing moves
                Done(Player.TryPushOff(back, BackPoint, Vector3.forward, 0.6f));
                yield return Frames(rates[run]);
                ends[run] = Position;
            }

            Assert.That(Vector3.Distance(ends[0], ends[1]), Is.LessThan(1e-3f), "frame-rate independent");
            Assert.That(ends[0].z - 0.6f, Is.EqualTo(0.6f).Within(0.02f), "0.6 m/s for 1 s, no drag");
        }

        [UnityTest]
        public IEnumerator ADrift_PassesA90cmDoorwayWithoutSnagging()
        {
            FloatAt(new Vector3(0f, 50.2f, 0.6f));
            Done(Player.TryPushOff(back, BackPoint, Vector3.forward));
            yield return Frames(150);
            Assert.That(Position.z, Is.GreaterThan(3.5f), "through the doorway at z = 3");
            Assert.That(Mathf.Abs(Position.x), Is.LessThan(0.01f));
            Assert.That(Player.FloatVelocity.z, Is.EqualTo(Player.Microgravity.MaxPushSpeed).Within(1e-4f), "no snag on the frame");
        }

        [UnityTest]
        public IEnumerator Grips_OnlyRegisteredHandholdsInReach_StopTheDrift_NeverMoveTheBody_AndBraceStopsAnApproach()
        {
            Collider gripped = null;
            Collider released = null;
            Player.Grabbed += c => gripped = c;
            Player.Released += c => released = c;

            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            Vector3 grip = new Vector3(-1.9f, 51.6f, 1.5f);
            Assert.That(Player.TryGrab(handhold, grip), Is.EqualTo(FloatActionResult.OutOfReach));
            Assert.That(Player.TryGrab(left, new Vector3(-2f, 50.2f + Eye, 0.6f)), Is.EqualTo(FloatActionResult.NotAHandhold), "a smooth wall is not grippable");

            Done(Player.TryPushOff(back, BackPoint, Vector3.forward, 0.5f));
            yield return Frames(90);
            Vector3 before = Position;
            Done(Player.TryGrab(handhold, grip));
            Assert.That(Position, Is.EqualTo(before), "a grip never moves the body to the handhold");
            Assert.That(gripped, Is.SameAs(handhold));
            Assert.That(Player.TryGrab(handhold, grip), Is.EqualTo(FloatActionResult.AlreadyHolding));
            yield return Frames(50);
            Assert.That(Player.FloatVelocity.magnitude, Is.LessThan(0.01f), "the grip stops the drift");

            Done(Player.Release());
            Assert.That(released, Is.SameAs(handhold));
            Assert.That(Player.Release(), Is.EqualTo(FloatActionResult.NotHolding));

            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            Done(Player.TryPushOff(back, BackPoint, Vector3.forward));
            Assert.That(Player.TryBrace(left, new Vector3(-2f, 50.2f + Eye, 0.6f)), Is.EqualTo(FloatActionResult.Done));
            Assert.That(Player.FloatVelocity.z, Is.EqualTo(1f).Within(1e-5f), "bracing a side wall gives no grip: the slide goes on");
            yield return Frames(80);
            Done(Player.TryBrace(dividerLeft, new Vector3(-1.3f, 50.2f + Eye, 2.95f)));
            Assert.That(Player.FloatVelocity.magnitude, Is.LessThan(1e-4f), "bracing the wall ahead stops the approach");
            Vector3 braced = Position;
            yield return Frames(25);
            Assert.That(Vector3.Distance(Position, braced), Is.LessThan(1e-4f));
        }

        [UnityTest]
        public IEnumerator PauseAndCinematicsKeepTheDrift_ClearMotionDisableTeleportAndTheRouteEndIt()
        {
            FloatAt(new Vector3(0f, 50.2f, 0.6f));
            Done(Player.TryPushOff(back, BackPoint, Vector3.forward, 0.5f));
            yield return Frames(5);
            gateOpen = false;
            Vector3 at = Position;
            Vector3 velocity = Player.FloatVelocity;
            yield return Frames(20);
            Assert.That(Vector3.Distance(Position, at), Is.LessThan(1e-5f), "control denied (pause, cinematic): the drift is suspended");
            Assert.That(Player.FloatVelocity, Is.EqualTo(velocity), "and kept for later");
            gateOpen = true;
            yield return Frames(40);
            Assert.That(Position.z, Is.GreaterThan(at.z + 0.3f), "resumed");

            Player.ClearMotion();
            Assert.That(Player.FloatVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(Player.IsFloating, Is.True);
            Vector3 stopped = Position;
            yield return Frames(160);
            Assert.That(Player.IsStranded, Is.True, "diagnostic only: mid-cabin and still after ClearMotion");
            Assert.That(Vector3.Distance(Position, stopped), Is.LessThan(1e-5f), "no hidden force moves a stranded player");

            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            Done(Player.TryPushOff(back, BackPoint, Vector3.forward));
            Player.enabled = false;
            Assert.That(Player.FloatVelocity, Is.EqualTo(Vector3.zero), "disabling clears the drift");
            Player.enabled = true;
            Assert.That(Player.IsFloating, Is.True);

            Player.EnterRoute(harness.Route);
            Assert.That(Player.IsFloating, Is.False, "the EVA tether keeps its own movement");
            Assert.That(Player.IsOnRoute, Is.True);
            Assert.That(Player.EnterFloat(), Is.EqualTo(FloatActionResult.OnRoute));
            Player.ExitRoute(new Vector3(-1.3f, 50.2f, 0.6f), 0f);
            Done(Player.EnterFloat());
            Place(new Vector3(-1.3f, 50.2f, 0.6f));
            Assert.That(Player.IsFloating, Is.False, "a teleport ends in walking");
        }

        // Codex 6881979236: the floor trap and the grip reach.
        [UnityTest]
        public IEnumerator ACeilingPush_LandsOutOfHandReach_AndOnlyARealFootContactRecovers()
        {
            FloatAt(new Vector3(0f, 51f, 1.5f));
            Assert.That(Player.TryPushFromFootContact(Vector3.up), Is.EqualTo(FloatActionResult.NoSurface), "no foot contact mid-air");
            Done(Player.TryPushOff(ceiling, new Vector3(0f, 53f, 1.5f), Vector3.down));
            yield return Frames(75);
            Assert.That(Position.y, Is.EqualTo(50f).Within(0.05f), "landed on the floor");
            Assert.That(Player.FloatVelocity.y, Is.EqualTo(0f).Within(1e-4f), "inelastic landing");
            Assert.That(Player.TryPushOff(floor, new Vector3(0f, 50f, 1.5f), Vector3.up), Is.EqualTo(FloatActionResult.OutOfReach), "the hands cannot reach the floor");
            yield return Frames(160);
            Assert.That(Player.IsStranded, Is.True, "without the feet this would be the reported trap");

            Vector3 resting = Player.FloatVelocity;
            Assert.That(Player.TryPushFromFootContact(Vector3.down), Is.EqualTo(FloatActionResult.IntoSurface));
            gateOpen = false;
            Assert.That(Player.TryPushFromFootContact(Vector3.up), Is.EqualTo(FloatActionResult.ControlDenied));
            gateOpen = true;
            Assert.That(Player.FloatVelocity, Is.EqualTo(resting), "refusals change nothing");
            Assert.That(resting, Is.EqualTo(Vector3.zero), "the landing left an exact stop");

            Done(Player.TryPushFromFootContact(Vector3.up, 0.5f));
            Assert.That(Player.FloatVelocity.y, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(Player.TryPushFromFootContact(Vector3.up), Is.EqualTo(FloatActionResult.Cooldown));
            yield return Frames(40);
            Assert.That(Position.y, Is.GreaterThan(50.3f), "recovered by a real foot push");
            Assert.That(Player.TryPushFromFootContact(Vector3.up), Is.EqualTo(FloatActionResult.NoSurface), "the feet have left the floor");

            Player.EnterRoute(harness.Route);
            Assert.That(Player.TryPushFromFootContact(Vector3.up), Is.EqualTo(FloatActionResult.NotFloating), "never on the EVA route");
        }

        [UnityTest]
        public IEnumerator AnOutwardGripNearTheReachLimit_HoldsTheBodyWithinReach_AndAnchorsTheHand()
        {
            FloatAt(new Vector3(-1.4f, 50.2f, 1.5f));
            Vector3 grip = new Vector3(-1.9f, 51.6f, 1.5f);
            Done(Player.TryPushOff(left, new Vector3(-2f, 50.2f + Eye, 1.5f), Vector3.right));
            float distance = 0f;
            for (int i = 0; i < 40 && distance < 0.83f; i++)
            {
                yield return null;
                distance = Vector3.Distance(Position + (Vector3.up * Eye), grip);
            }

            Assert.That(distance, Is.InRange(0.83f, 0.85f), "near the reach limit, still moving outward at 1 m/s");
            Done(Player.TryGrab(handhold, grip));
            Vector3 anchor = Player.HeldGripPoint.Value;
            Assert.That(Vector3.Distance(anchor, grip), Is.LessThan(0.01f), "the anchor is the verified grip point");
            for (int i = 0; i < 50; i++)
            {
                yield return null;
                Assert.That(Player.HeldHandhold, Is.SameAs(handhold));
                Assert.That(Vector3.Distance(Position + (Vector3.up * Eye), anchor), Is.LessThanOrEqualTo(Player.Microgravity.ReachMetres + 1e-3f),
                    "the arm holds the body within reach (frame " + i + ")");
            }

            Assert.That(Player.FloatVelocity.magnitude, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator AGripSettle_At30And240Fps_EndsInTheSamePlace()
        {
            var ends = new Vector3[2];
            int[] rates = { 30, 240 };
            Vector3 grip = new Vector3(-1.9f, 51.6f, 1.5f);
            for (int run = 0; run < 2; run++)
            {
                int fps = rates[run];
                Time.captureDeltaTime = 1f / fps;
                FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
                yield return null; // the new frame time applies from here
                Done(Player.TryPushOff(back, BackPoint, Vector3.forward, 0.5f));
                yield return Frames(Mathf.RoundToInt(1.8f * fps));
                Done(Player.TryGrab(handhold, grip));
                yield return Frames(fps);
                Assert.That(Player.HeldHandhold, Is.SameAs(handhold));
                ends[run] = Position;
            }

            Assert.That(Vector3.Distance(ends[0], ends[1]), Is.LessThan(1e-3f), "the grip settle is frame-rate independent");
        }
    }
}
