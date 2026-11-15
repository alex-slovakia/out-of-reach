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

        /// <summary>The side-wall point level with the eye, for the second hand.</summary>
        private Vector3 SidePoint => new Vector3(-2f, Position.y + Eye, Position.z);

        [UnityTest]
        public IEnumerator APairPush_ChecksBothHandsFirst_AndAnyRefusalChangesNothing()
        {
            int pushes = 0;
            Player.Pushed += _ => pushes++;
            Place(new Vector3(-1.3f, 50.2f, 0.6f));
            Assert.That(Player.TryPushOffPair(back, BackPoint, Vector3.forward, 1f, left, SidePoint, Vector3.right, 1f),
                Is.EqualTo(FloatActionResult.NotFloating));
            Done(Player.EnterFloat());
            Vector3 point = BackPoint;
            Vector3 side = SidePoint;

            // Hand A is valid every time; each refusal below comes from hand B.
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, null, side, Vector3.right, 1f), Is.EqualTo(FloatActionResult.NoSurface));
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, left, side, Vector3.left, 1f), Is.EqualTo(FloatActionResult.IntoSurface));
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, front, new Vector3(-1.3f, 50.2f + Eye, 6f), Vector3.back, 1f),
                Is.EqualTo(FloatActionResult.OutOfReach));
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, left, side, Vector3.zero, 1f), Is.EqualTo(FloatActionResult.InvalidInput));
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, left, side, Vector3.right, 0f), Is.EqualTo(FloatActionResult.InvalidInput));
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, left, new Vector3(float.NaN, 0f, 0f), Vector3.right, 1f),
                Is.EqualTo(FloatActionResult.InvalidInput));
            GameObject blocker = harness.AddWall(new Vector3(-1.7f, 50.2f + Eye, 0.6f), new Vector3(0.1f, 0.4f, 0.4f));
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, left, side, Vector3.right, 1f), Is.EqualTo(FloatActionResult.Occluded));
            blocker.SetActive(false);
            Physics.SyncTransforms();

            // A refused hand A is reported first even when hand B is fine; the control gate covers both.
            Assert.That(Player.TryPushOffPair(back, point, Vector3.back, 1f, left, side, Vector3.right, 1f), Is.EqualTo(FloatActionResult.IntoSurface));
            gateOpen = false;
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, left, side, Vector3.right, 1f), Is.EqualTo(FloatActionResult.ControlDenied));
            gateOpen = true;

            Assert.That(Player.FloatVelocity, Is.EqualTo(Vector3.zero), "no refusal changed the velocity");
            Assert.That(pushes, Is.Zero, "no refusal raised Pushed");
            Done(Player.TryPushOff(back, point, Vector3.forward, 0.5f));
            Assert.That(pushes, Is.EqualTo(1), "no refusal started the cooldown");
            Assert.That(Player.TryPushOffPair(back, point, Vector3.forward, 1f, left, side, Vector3.right, 1f), Is.EqualTo(FloatActionResult.Cooldown),
                "single and pair pushes share one cooldown");
            Assert.That(pushes, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator APairPush_AddsBothHands_CapsTheSumToOneFullPush_AndRaisesPushedOnce()
        {
            int pushes = 0;
            Player.Pushed += _ => pushes++;
            FloatAt(new Vector3(-1.3f, 50.2f, 0.6f));
            float max = Player.Microgravity.MaxPushSpeed;

            // Back wall pushing forward and side wall pushing right, both full: the diagonal sum is capped to one full push.
            Done(Player.TryPushOffPair(back, BackPoint, Vector3.forward, 1f, left, SidePoint, Vector3.right, 1f));
            Assert.That(pushes, Is.EqualTo(1), "one Pushed event");
            Vector3 diagonal = new Vector3(1f, 0f, 1f).normalized * max;
            Assert.That(Vector3.Distance(Player.FloatVelocity, diagonal), Is.LessThan(1e-5f));

            MicrogravityLocomotion math = Player.Microgravity;
            Assert.That(Vector3.Distance(math.PushPair(Vector3.zero, Vector3.forward, 0.35f, Vector3.forward, 0.35f), new Vector3(0f, 0f, 0.7f * max)),
                Is.LessThan(1e-5f), "two partial hands are stronger than one");
            Assert.That(Vector3.Distance(math.PushPair(Vector3.zero, Vector3.forward, 1f, Vector3.forward, 1f), new Vector3(0f, 0f, max)),
                Is.LessThan(1e-5f), "never stronger than one full push");
            Assert.That(math.PushPair(new Vector3(0f, 0f, 1.2f), Vector3.forward, 1f, Vector3.forward, 1f).magnitude,
                Is.EqualTo(math.MaxSpeed).Within(1e-5f), "the total speed stays capped");
            Assert.That(Vector3.Distance(math.PushPair(Vector3.zero, Vector3.forward, 1f, Vector3.back, 1f), Vector3.zero), Is.LessThan(1e-5f),
                "opposite hands cancel");
            yield return null;
        }

        [UnityTest]
        public IEnumerator APairPush_ReleasesAHeldHandholdOnce_OnlyWhenItSucceeds()
        {
            int released = 0;
            Player.Released += _ => released++;
            FloatAt(new Vector3(-1.3f, 50.2f, 1.5f));
            Done(Player.TryGrab(handhold, new Vector3(-1.9f, 51.6f, 1.5f)));
            float eye = Position.y + Eye;
            Vector3 a = new Vector3(-2f, eye, 1.4f);
            Vector3 b = new Vector3(-2f, eye, 1.6f);

            Assert.That(Player.TryPushOffPair(left, a, Vector3.right, 1f, left, b, Vector3.left, 1f), Is.EqualTo(FloatActionResult.IntoSurface));
            Assert.That(Player.HeldHandhold, Is.SameAs(handhold), "a refused pair keeps the grip");
            Assert.That(released, Is.Zero);

            Done(Player.TryPushOffPair(left, a, Vector3.right, 1f, left, b, Vector3.right, 1f));
            Assert.That(Player.HeldHandhold, Is.Null);
            Assert.That(released, Is.EqualTo(1), "released once");
            Assert.That(Vector3.Distance(Player.FloatVelocity, new Vector3(Player.Microgravity.MaxPushSpeed, 0f, 0f)), Is.LessThan(1e-5f),
                "two full hands on one wall give one full push");
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

        // MICRO-FLOAT-02 (Codex 6882323154): the grip is revalidated every frame; foot support must be real.
        [UnityTest]
        public IEnumerator AHeldGrip_IsReleasedOnce_WhenItsAnchorLeavesReach_TurnsTrigger_GoesInactive_OrIsCutOff()
        {
            int releases = 0;
            Player.Released += c => releases++;
            Vector3 grip = new Vector3(-1.9f, 51.6f, 1.5f);
            Vector3 home = handhold.transform.position;
            GameObject door = harness.AddWall(new Vector3(-1.6f, 51.725f, 1.5f), new Vector3(0.05f, 0.3f, 0.3f));
            door.SetActive(false);
            Physics.SyncTransforms();

            for (int scenario = 0; scenario < 4; scenario++)
            {
                FloatAt(new Vector3(-1.3f, 50.2f, 1.5f));
                Done(Player.TryGrab(handhold, grip));
                yield return Frames(3);
                Assert.That(Player.HeldHandhold, Is.SameAs(handhold), "held before scenario " + scenario);
                releases = 0;
                Vector3 before = Position;

                switch (scenario)
                {
                    case 0:
                        handhold.transform.position = home + new Vector3(0f, 0f, 1f); // anchor about 0.65 m -> 1.19 m away
                        break;
                    case 1:
                        handhold.isTrigger = true;
                        break;
                    case 2:
                        handhold.enabled = false;
                        break;
                    default:
                        door.SetActive(true); // closes between the hand and the anchor
                        break;
                }

                Physics.SyncTransforms();
                yield return Frames(3);
                Assert.That(Player.HeldHandhold, Is.Null, "released in scenario " + scenario);
                Assert.That(releases, Is.EqualTo(1), "exactly once in scenario " + scenario);
                Assert.That(Vector3.Distance(Position, before), Is.LessThan(1e-3f), "no pull or teleport in scenario " + scenario);

                handhold.transform.position = home;
                handhold.isTrigger = false;
                handhold.enabled = true;
                door.SetActive(false);
                Physics.SyncTransforms();
            }
        }

        [UnityTest]
        public IEnumerator AFootPush_NeedsRealSupportBelow_NotASideWallABareEdgeAnOwnedPartOrATrigger()
        {
            FloatAt(new Vector3(-1.67f, 51f, 1f));
            Assert.That(Player.TryPushFromFootContact(Vector3.right), Is.EqualTo(FloatActionResult.NoSurface), "touching a side wall is not foot support");

            GameObject ledge = harness.AddWall(new Vector3(0.45f, 50.65f, 1.5f), new Vector3(0.4f, 0.1f, 0.4f));
            FloatAt(new Vector3(0f, 50.6f, 1.5f));
            Assert.That(Player.TryPushFromFootContact(Vector3.up), Is.EqualTo(FloatActionResult.NoSurface), "a bare edge under the side of the foot is not support");
            ledge.SetActive(false);
            Physics.SyncTransforms();

            GameObject boot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boot.transform.SetParent(Player.transform, false);
            boot.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            boot.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);
            GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.transform.SetParent(Player.transform, false);
            chest.transform.localPosition = new Vector3(0f, Eye, -0.2f);
            chest.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            CharacterController capsule = Player.GetComponent<CharacterController>();
            Physics.IgnoreCollision(boot.GetComponent<Collider>(), capsule);
            Physics.IgnoreCollision(chest.GetComponent<Collider>(), capsule);
            FloatAt(new Vector3(-1.3f, 51f, 0.6f));
            Assert.That(Player.TryPushFromFootContact(Vector3.up), Is.EqualTo(FloatActionResult.NoSurface), "an owned solid part is not foot support");
            Done(Player.TryPushOff(back, BackPoint, Vector3.forward, 0.2f));
            Object.DestroyImmediate(boot);
            Object.DestroyImmediate(chest);

            FloatAt(new Vector3(0f, 51f, 1.5f));
            Collider pad = harness.AddWall(new Vector3(0f, 50.95f, 1.5f), new Vector3(0.6f, 0.08f, 0.6f)).GetComponent<Collider>();
            pad.isTrigger = true;
            Physics.SyncTransforms();
            Assert.That(Player.TryPushFromFootContact(Vector3.up), Is.EqualTo(FloatActionResult.NoSurface), "a trigger under the feet is not support");

            pad.isTrigger = false;
            Physics.SyncTransforms();
            Done(Player.TryPushFromFootContact(Vector3.up, 0.3f));
            Assert.That(Player.FloatVelocity.y, Is.EqualTo(0.3f).Within(1e-5f), "the same pad, solid and under the feet, is real support");
            yield return null;
        }

        // MICRO-FLOAT-03 (Codex 6882492748): the held ray must reach the stored anchor, not another face of the same collider.
        [UnityTest]
        public IEnumerator AHeldGrip_IsReleased_WhenItsAnchorTurnsToTheFarSideOfTheSameHandhold()
        {
            int releases = 0;
            Player.Released += c => releases++;
            FloatAt(new Vector3(-1.3f, 50.2f, 1.5f));
            Done(Player.TryGrab(handhold, new Vector3(-1.9f, 51.6f, 1.5f)));
            yield return Frames(3);
            Assert.That(Player.HeldHandhold, Is.SameAs(handhold));
            releases = 0;
            Vector3 before = Position;

            handhold.transform.Rotate(0f, 180f, 0f, Space.World);
            Physics.SyncTransforms();
            Vector3 turnedAnchor = Player.HeldGripPoint.Value;
            Vector3 eye = Position + (Vector3.up * Eye);
            Assert.That(turnedAnchor.x, Is.LessThan(-1.95f), "the anchor now sits on the far face");
            Assert.That(Vector3.Distance(eye, turnedAnchor), Is.LessThan(Player.Microgravity.ReachMetres), "still within reach, so only the anchor-point check can catch it");

            yield return Frames(3);
            Assert.That(Player.HeldHandhold, Is.Null, "no hand through the box");
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(Vector3.Distance(Position, before), Is.LessThan(1e-3f), "no pull or teleport");
        }

        /// <summary>Stands the player on the cabin floor at (x, z) and lets the walking controller settle there.</summary>
        private IEnumerator StandOnFloor(float x, float z)
        {
            Place(new Vector3(x, 50.02f, z));
            yield return Frames(10);
            Assert.That(Position.y, Is.EqualTo(50f).Within(0.05f), "standing on the floor");
        }

        // MICRO-ENTRY (Codex 6883884848): entering float from real foot support where no surface is within hand reach.
        [UnityTest]
        public IEnumerator FootSupportEntry_StartsFloatOnTheCentreAisle_AtRest_WithoutAnyMove_AndIsIdempotent()
        {
            yield return StandOnFloor(0f, 1.5f);
            Assert.That(Player.EnterFloat(), Is.EqualTo(FloatActionResult.NoSurface), "EnterFloat unchanged: no surface within hand reach mid-aisle");
            Assert.That(Player.IsFloating, Is.False);

            Vector3 standing = Position;
            Done(Player.EnterFloatFromFootSupport());
            Assert.That(Player.IsFloating, Is.True);
            Assert.That(Player.FloatVelocity, Is.EqualTo(Vector3.zero), "no impulse");
            Assert.That(Player.HeldHandhold, Is.Null);
            Assert.That(Vector3.Distance(Position, standing), Is.LessThan(1e-6f), "no teleport");
            yield return Frames(25);
            Assert.That(Vector3.Distance(Position, standing), Is.LessThan(1e-4f), "no gravity and no drift without a push");

            Done(Player.EnterFloatFromFootSupport());
            Assert.That(Player.IsFloating, Is.True, "repeat: idempotent");
            Assert.That(Player.FloatVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(Vector3.Distance(Position, standing), Is.LessThan(1e-4f));

            Done(Player.TryPushFromFootContact(Vector3.up, 0.4f));
            Assert.That(Player.FloatVelocity.y, Is.EqualTo(0.4f).Within(1e-5f), "the floor push stays the separate, deliberate command");
        }

        [UnityTest]
        public IEnumerator FootSupportEntry_IsRefused_MidAir_OverATrigger_WhenControlIsDenied_AndOnTheRoute()
        {
            Place(new Vector3(0f, 51f, 1.5f));
            Vector3 midAir = Position;
            Assert.That(Player.EnterFloatFromFootSupport(), Is.EqualTo(FloatActionResult.NoSurface), "never granted mid-air");
            Assert.That(Player.IsFloating, Is.False);
            Assert.That(Vector3.Distance(Position, midAir), Is.LessThan(1e-6f), "a refusal moves nothing");

            Collider pad = harness.AddWall(new Vector3(0f, 50.95f, 1.5f), new Vector3(0.6f, 0.08f, 0.6f)).GetComponent<Collider>();
            pad.isTrigger = true;
            Physics.SyncTransforms();
            Assert.That(Player.EnterFloatFromFootSupport(), Is.EqualTo(FloatActionResult.NoSurface), "a trigger under the feet is not support");
            Assert.That(Player.IsFloating, Is.False);
            pad.gameObject.SetActive(false);
            Physics.SyncTransforms();

            yield return Frames(40);
            Assert.That(Position.y, Is.EqualTo(50f).Within(0.05f), "walking gravity still applied after the refusals");

            gateOpen = false;
            Assert.That(Player.EnterFloatFromFootSupport(), Is.EqualTo(FloatActionResult.ControlDenied), "paused or in a cinematic");
            Assert.That(Player.IsFloating, Is.False);
            gateOpen = true;

            Player.EnterRoute(harness.Route);
            Assert.That(Player.EnterFloatFromFootSupport(), Is.EqualTo(FloatActionResult.OnRoute));
            Assert.That(Player.IsFloating, Is.False);
            Player.ExitRoute(new Vector3(0f, 50.02f, 1.5f), 0f);
            Physics.SyncTransforms();
            yield return Frames(10);

            Done(Player.EnterFloatFromFootSupport());
            Assert.That(Player.IsFloating, Is.True, "landed on real support: granted");
        }

        [UnityTest]
        public IEnumerator AfterAFootSupportEntry_APushUpStopsAtTheCeiling_WithoutPenetrating()
        {
            yield return StandOnFloor(0f, 1.5f);
            Done(Player.EnterFloatFromFootSupport());
            Done(Player.TryPushFromFootContact(Vector3.up, 1f));
            yield return Frames(120);

            CharacterController capsule = Player.GetComponent<CharacterController>();
            float top = Position.y + capsule.center.y + (capsule.height * 0.5f);
            Assert.That(top, Is.LessThanOrEqualTo(53f + 1e-3f), "the head stops at the ceiling (bottom face y = 53)");
            Assert.That(top, Is.GreaterThan(52.9f), "it got there");
            Assert.That(Player.FloatVelocity.y, Is.EqualTo(0f).Within(1e-4f), "inelastic stop");
            Assert.That(Player.IsFloating, Is.True);
        }
    }
}
