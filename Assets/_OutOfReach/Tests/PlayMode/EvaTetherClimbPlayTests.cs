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
    /// EVA-TETHER-01: the EVA route is travelled hand over hand. The body only moves while W/S grips and pulls: it
    /// starts from rest, surges through every pull (slowest as the gloves change over on a rung, fastest mid-pull),
    /// averages the route speed over whole pulls, brakes to rest on release, passes through rest before reversing,
    /// and stops at once at either end. Never a jump, never off the line.
    /// </summary>
    public sealed class EvaTetherClimbPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private static readonly Vector2 Forward = new Vector2(0f, 1f);
        private static readonly Vector2 Back = new Vector2(0f, -1f);

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

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        private IEnumerator GoOutside()
        {
            harness.ReachStabilized();
            MissionSession session = harness.Session;
            Require(session.Eva.TryPrepareSuit());
            Require(session.Eva.TryBeginEva());
            Require(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds));
            Require(session.Eva.TryGoOutside());
            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }

            Assert.That(harness.Player.IsOnRoute, Is.True);
            harness.FacePlayerTowards(harness.Player.transform.position + Vector3.right);
            yield return null;
            Assert.That(harness.Player.RouteSpeedMetresPerSecond, Is.EqualTo(0f), "both gloves at rest on the rope");
            Assert.That(harness.Player.RouteMotionSense, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator HeldForward_StartsFromRest_SurgesEveryPull_AndAveragesTheRouteSpeed()
        {
            yield return GoOutside();
            TetheredRoute route = harness.Player.ActiveRoute;
            float peak = route.SpeedMetresPerSecond * RouteMath.PullSpeedFactor(0.5f, route.PullSurge);
            var speeds = new List<float>();
            var phases = new List<float>();
            harness.Input.Move = Forward;
            float previous = harness.Player.RouteDistanceMetres;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                float now = harness.Player.RouteDistanceMetres;
                Assert.That(now, Is.GreaterThanOrEqualTo(previous), "W never moves the body backwards in frame " + i);
                Assert.That(now - previous, Is.LessThanOrEqualTo((peak * FrameSeconds) + 1e-4f), "no jump in frame " + i);
                speeds.Add(harness.Player.RouteSpeedMetresPerSecond);
                phases.Add(harness.Player.RouteStrokePhase);
                previous = now;
            }

            harness.Input.Move = Vector2.zero;
            Assert.That(speeds[0], Is.LessThan(0.5f * route.SpeedMetresPerSecond), "the first frame of a grip is not full speed");

            // After the grip ramp (frames 15+): slow near the change-over, fast mid-pull.
            float slow = float.PositiveInfinity;
            float fast = 0f;
            for (int i = 15; i < speeds.Count; i++)
            {
                if (phases[i] < 0.1f || phases[i] > 0.9f)
                {
                    slow = Mathf.Min(slow, speeds[i]);
                }

                if (phases[i] > 0.4f && phases[i] < 0.6f)
                {
                    fast = Mathf.Max(fast, speeds[i]);
                }
            }

            Assert.That(fast, Is.GreaterThan(slow * 1.4f), "each pull surges: mid-pull is clearly faster than the change-over");
            Assert.That(fast, Is.LessThanOrEqualTo(peak + 1e-3f));

            // Whole pulls average the route speed: time 1.6 s of steady pulling from frame 20.
            float travelled = 0f;
            for (int i = 20; i < 100; i++)
            {
                travelled += speeds[i] * FrameSeconds;
            }

            Assert.That(travelled / (80 * FrameSeconds), Is.EqualTo(route.SpeedMetresPerSecond).Within(0.08f * route.SpeedMetresPerSecond));
        }

        [UnityTest]
        public IEnumerator Release_TheHoldingGloveBrakesTheBodyToRest_WithinTheBrakeTime()
        {
            yield return GoOutside();
            TetheredRoute route = harness.Player.ActiveRoute;
            harness.Input.Move = Forward;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }

            float released = harness.Player.RouteDistanceMetres;
            harness.Input.Move = Vector2.zero;
            int frames = 0;
            while (harness.Player.RouteMotionSense != 0 && frames < 60)
            {
                yield return null;
                frames++;
            }

            Assert.That(harness.Player.RouteMotionSense, Is.EqualTo(0));
            Assert.That(frames * FrameSeconds, Is.LessThanOrEqualTo(route.BrakeSeconds + (2 * FrameSeconds)), "brakes within the brake time");
            float braking = harness.Player.RouteDistanceMetres - released;
            Assert.That(braking, Is.GreaterThan(0f), "a moving body glides into the brake, it does not stop dead");
            Assert.That(braking, Is.LessThan(0.25f), "and the glove stops it within a short distance");

            float rest = harness.Player.RouteDistanceMetres;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(rest), "no drift at rest on the rope");
            Assert.That(harness.Player.RouteSpeedMetresPerSecond, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Reversal_PassesThroughRest_BeforeMovingBack()
        {
            yield return GoOutside();
            harness.Input.Move = Forward;
            for (int i = 0; i < 50; i++)
            {
                yield return null;
            }

            harness.Input.Move = Back;
            bool sawRest = false;
            float furthest = harness.Player.RouteDistanceMetres;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (harness.Player.RouteMotionSense == 0 || harness.Player.RouteSpeedMetresPerSecond == 0f)
                {
                    sawRest = true;
                }

                if (!sawRest)
                {
                    Assert.That(harness.Player.RouteMotionSense, Is.EqualTo(1), "still braking forwards in frame " + i);
                    furthest = Mathf.Max(furthest, harness.Player.RouteDistanceMetres);
                }
                else if (harness.Player.RouteMotionSense != 0)
                {
                    Assert.That(harness.Player.RouteMotionSense, Is.EqualTo(-1), "then S pulls back in frame " + i);
                }
            }

            harness.Input.Move = Vector2.zero;
            Assert.That(sawRest, Is.True, "no instant reversal: the body comes to rest first");
            Assert.That(harness.Player.RouteDistanceMetres, Is.LessThan(furthest - 0.2f), "S then moves the body back");
        }

        [UnityTest]
        public IEnumerator TheEndAnchor_StopsTheBodyAtOnce()
        {
            yield return GoOutside();
            harness.Input.Move = Forward;
            int frames = 0;
            while (!harness.Player.IsAtRouteEnd && frames < 600)
            {
                yield return null;
                frames++;
            }

            Assert.That(harness.Player.IsAtRouteEnd, Is.True);
            Assert.That(harness.Player.RouteMotionSense, Is.EqualTo(0), "the end anchor leaves no pull");
            Assert.That(harness.Player.RouteSpeedMetresPerSecond, Is.EqualTo(0f));
            Vector3 atEnd = harness.Player.transform.position;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            harness.Input.Move = Vector2.zero;
            Assert.That(Vector3.Distance(harness.Player.transform.position, atEnd), Is.LessThan(1e-6f), "no bounce or jitter at the end");
        }

        [UnityTest]
        public IEnumerator Pause_FreezesThePull_AndResumeContinuesWithoutAJump()
        {
            yield return GoOutside();
            TetheredRoute route = harness.Player.ActiveRoute;
            float peak = route.SpeedMetresPerSecond * RouteMath.PullSpeedFactor(0.5f, route.PullSurge);
            harness.Input.Move = Forward;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            float before = harness.Player.RouteDistanceMetres;
            harness.Runtime.SetPaused(true);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.That(harness.Player.RouteDistanceMetres, Is.EqualTo(before), "paused mid-pull");
            harness.Runtime.SetPaused(false);
            yield return null;
            Assert.That(harness.Player.RouteDistanceMetres - before, Is.LessThanOrEqualTo((peak * FrameSeconds) + 1e-4f), "no catch-up jump");
            harness.Input.Move = Vector2.zero;
        }
    }
}
