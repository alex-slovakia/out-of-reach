using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// Free-looking traversal of the EVA tether (defect from desktop play, 2026-11-14): the feet stay on the line,
    /// looking never moves the player, W and S follow the line towards the end the player roughly faces, nothing
    /// jumps at corners or on vertical stretches, and both ends stop the player cleanly. Unlike the full-EVA test,
    /// the view is deliberately NOT aimed along the waypoints.
    /// </summary>
    public sealed class EvaTraversalPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const float OnLineTolerance = 1e-3f;

        private static readonly Vector2 Forward = new Vector2(0f, 1f);
        private static readonly Vector2 Back = new Vector2(0f, -1f);

        private readonly List<GameObject> extras = new List<GameObject>();
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
            foreach (GameObject extra in extras)
            {
                Object.DestroyImmediate(extra);
            }

            extras.Clear();
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

        private TetheredRoute BuildRoute(Vector3[] points)
        {
            var routeObject = new GameObject("TestRoute");
            extras.Add(routeObject);
            var waypoints = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var waypoint = new GameObject("Waypoint" + i);
                waypoint.transform.SetParent(routeObject.transform, false);
                waypoint.transform.position = points[i];
                waypoints[i] = waypoint.transform;
            }

            TetheredRoute route = routeObject.AddComponent<TetheredRoute>();
            route.Configure(waypoints, 0.5f, 1f);
            return route;
        }

        /// <summary>Stabilizes the ship, suits up, cycles the airlock and goes outside onto the given route (or the harness route).</summary>
        private IEnumerator GoOutside(TetheredRoute route = null)
        {
            if (route != null)
            {
                harness.Runtime.ConfigureEva(new[] { harness.InnerGate, harness.OuterGate }, route, harness.Runtime.EvaReentryPoint);
            }

            harness.ReachStabilized();
            MissionSession session = harness.Session;
            Require(session.Eva.TryPrepareSuit());
            Require(session.Eva.TryBeginEva());
            Require(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds));
            Require(session.Eva.TryGoOutside());
            yield return Frames(3);
            Assert.That(harness.Player.IsOnRoute, Is.True);
            Assert.That(harness.Player.ActiveRoute, Is.SameAs(route != null ? route : harness.Route));
            Assert.That(harness.Player.IsAtRouteStart, Is.True);
        }

        private float OffLine(Vector3[] points)
        {
            Vector3 onLine = RouteMath.ClosestPoint(points, harness.Player.transform.position, out float unused);
            return Vector3.Distance(onLine, harness.Player.transform.position);
        }

        /// <summary>Holds a move input; every frame the feet must be on the line and move at most one frame's route speed.</summary>
        private IEnumerator Hold(Vector2 move, int frames, Vector3[] points)
        {
            float maxStep = (harness.Player.ActiveRoute.SpeedMetresPerSecond * FrameSeconds) + OnLineTolerance;
            Vector3 previous = harness.Player.transform.position;
            harness.Input.Move = move;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                Vector3 now = harness.Player.transform.position;
                Assert.That(Vector3.Distance(now, previous), Is.LessThanOrEqualTo(maxStep), "no jump in frame " + i);
                Assert.That(OffLine(points), Is.LessThan(OnLineTolerance), "the feet stay on the tether line in frame " + i);
                previous = now;
            }

            harness.Input.Move = Vector2.zero;
        }

        private void Face(Vector3 direction)
        {
            harness.FacePlayerTowards(harness.Player.transform.position + direction);
        }

        [UnityTest]
        public IEnumerator LookingAround_OrSidewaysKeys_NeverMoveThePlayer()
        {
            yield return GoOutside();
            Vector3 start = harness.Player.transform.position;

            for (int i = 0; i < 20; i++)
            {
                harness.Input.QueueLook(37f, i % 2 == 0 ? -200f : 200f);
                yield return null;
            }

            Assert.That(Vector3.Distance(harness.Player.transform.position, start), Is.LessThan(1e-6f), "look only");

            harness.Input.Move = new Vector2(1f, 0f);
            yield return Frames(20);
            harness.Input.Move = new Vector2(0.05f, 0.05f);
            yield return Frames(20);
            harness.Input.Move = Vector2.zero;

            Assert.That(Vector3.Distance(harness.Player.transform.position, start), Is.LessThan(1e-6f), "sideways keys and a resting stick");
        }

        [UnityTest]
        public IEnumerator ForwardWhileLookingStraightDownOrUp_FollowsTheLine_WithoutVerticalTravel()
        {
            yield return GoOutside();
            Vector3[] points = RepairLoopHarness.RouteWaypoints;
            float level = points[0].y;
            Face(Vector3.right);
            harness.Input.QueueLook(0f, -200f);
            yield return Frames(2);
            Assert.That(harness.Player.PitchDegrees, Is.EqualTo(-harness.Player.MaxPitchDegrees), "looking straight down");

            yield return Hold(Forward, 100, points);

            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(2f).Within(0.03f), "1 m/s for 2 s");
            Assert.That(harness.Player.transform.position.y, Is.EqualTo(level).Within(1e-4f), "no up or down travel from the view");

            harness.Input.QueueLook(0f, 400f);
            yield return Frames(1);
            Assert.That(harness.Player.PitchDegrees, Is.EqualTo(harness.Player.MaxPitchDegrees), "looking straight up");
            yield return Hold(Forward, 100, points);

            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(4f).Within(0.05f), "round the corner at 3 m without turning the view");
            Assert.That(harness.Player.transform.position.y, Is.EqualTo(level).Within(1e-4f));
            Assert.That(harness.Player.RouteDirection, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FacingBack_ReturnsTowardsTheHatch_SidewaysKeepsTheSense_AndBackMovesTheOtherWay()
        {
            yield return GoOutside();
            Vector3[] points = RepairLoopHarness.RouteWaypoints;
            Face(Vector3.right);
            yield return Hold(Forward, 100, points);
            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(2f).Within(0.03f));

            Face(Vector3.left);
            yield return Hold(Forward, 50, points);
            Assert.That(harness.Player.RouteDirection, Is.EqualTo(-1), "facing the hatch, W leads back");
            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(1f).Within(0.03f));

            Face(Vector3.forward);
            yield return Hold(Forward, 25, points);
            Assert.That(harness.Player.RouteDirection, Is.EqualTo(-1), "looking sideways does not flip the sense");
            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(0.5f).Within(0.03f));

            yield return Hold(Back, 25, points);
            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(1f).Within(0.03f), "S moves the other way");
        }

        [UnityTest]
        public IEnumerator ADescendingTether_IsTraversedWithALevelView_AndStopsCleanlyAtBothEnds()
        {
            Vector3[] points =
            {
                new Vector3(0f, 0.02f, -8f), new Vector3(2f, 0.02f, -8f), new Vector3(2f, -2.98f, -8f), new Vector3(3f, -2.98f, -8f),
            };
            TetheredRoute route = BuildRoute(points);
            yield return GoOutside(route);
            Face(Vector3.right);
            Assert.That(harness.Player.PitchDegrees, Is.EqualTo(0f), "level view");

            yield return Hold(Forward, 350, points);

            Assert.That(harness.Player.IsAtRouteEnd, Is.True, "down the vertical stretch without looking down");
            Assert.That(Vector3.Distance(harness.Player.transform.position, points[3]), Is.LessThan(OnLineTolerance));

            Vector3 atEnd = harness.Player.transform.position;
            yield return Hold(Forward, 30, points);
            Assert.That(Vector3.Distance(harness.Player.transform.position, atEnd), Is.LessThan(1e-6f), "the end stops the player, no jitter");

            Face(Vector3.left);
            yield return Hold(Forward, 350, points);

            Assert.That(harness.Player.IsAtRouteStart, Is.True, "back up to the hatch facing it");
            Assert.That(Vector3.Distance(harness.Player.transform.position, points[0]), Is.LessThan(OnLineTolerance));
        }

        [UnityTest]
        public IEnumerator Pause_StopsTraversal_AndResumeContinuesFromTheSamePlace()
        {
            yield return GoOutside();
            Vector3[] points = RepairLoopHarness.RouteWaypoints;
            Face(Vector3.right);
            yield return Hold(Forward, 25, points);
            float before = harness.Player.RouteDistanceMetres;

            harness.Runtime.SetPaused(true);
            harness.Input.Move = Forward;
            yield return Frames(30);
            harness.Input.Move = Vector2.zero;
            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(before), "paused");

            harness.Runtime.SetPaused(false);
            yield return Hold(Forward, 25, points);
            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(before + 0.5f).Within(0.03f));
        }
    }
}
