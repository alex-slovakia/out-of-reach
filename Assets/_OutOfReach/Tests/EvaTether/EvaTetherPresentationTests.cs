using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Composition.Editor;
using OutOfReach.Player;
using OutOfReach.Presentation.Hands;
using UnityEngine;

namespace OutOfReach.Tests.EvaTether
{
    /// <summary>
    /// EVA-TETHER-01, presentation side: the rope follows the real route at shoulder height on the hull side with
    /// bounded sag and anchors; the glove lattice is the gameplay lattice; a closed glove holds the rope inside its fist
    /// with the delivered forearm length and a natural wrist; the installer is idempotent and replaces the old guide.
    /// </summary>
    public sealed class EvaTetherPresentationTests
    {
        // The delivered EVA route of the saved Bootstrap (feet waypoints, metres).
        private static readonly Vector3[] Delivered =
        {
            new Vector3(0f, 0f, 22f), new Vector3(0f, 1.2f, 22.8f), new Vector3(3.8f, 1.2f, 22.8f),
            new Vector3(4.35f, 0.9f, 21.7f), new Vector3(4.35f, 0.9f, 14.35f), new Vector3(3.9f, 0.9f, 14.1f),
            new Vector3(3.75f, 0.9f, 13.15f),
        };

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                if (item)
                {
                    Object.DestroyImmediate(item);
                }
            }

            created.Clear();
        }

        private Transform[] Waypoints(Vector3[] positions, out GameObject root)
        {
            root = new GameObject("Tether test route");
            created.Add(root);
            var points = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                points[i] = new GameObject("Station " + i).transform;
                points[i].SetParent(root.transform, false);
                points[i].position = positions[i];
            }

            return points;
        }

        private EvaTetherRope Rope(Transform[] points)
        {
            var go = new GameObject("Tether test rope");
            created.Add(go);
            EvaTetherRope rope = go.AddComponent<EvaTetherRope>();
            rope.Configure(points, 0.5f, 0.14f, null, null, null);
            return rope;
        }

        [Test]
        public void GloveLattice_IsTheGameplayLattice()
        {
            for (int sense = -1; sense <= 1; sense += 2)
            {
                for (float d = -0.3f; d < 16f; d += 0.0137f)
                {
                    Assert.That(EvaTetherClimbMath.HoldingRung(d, sense, 0.5f, 0.14f),
                        Is.EqualTo(RouteMath.HoldingRung(d, sense, 0.5f, 0.14f)).Within(1e-5f));
                    Assert.That(EvaTetherClimbMath.StrokePhase(d, sense, 0.5f, 0.14f),
                        Is.EqualTo(RouteMath.HandStrokePhase(d, sense, 0.5f, 0.14f)).Within(1e-5f));
                }
            }
        }

        [Test]
        public void TheLeadingGlove_HoldsOneFixedRungThroughAPull_AndTheGlovesAlternate()
        {
            const float stroke = 0.5f;
            const float near = 0.14f;
            for (int sense = -1; sense <= 1; sense += 2)
            {
                float lastRung = float.NaN;
                EvaTetherHand lastHand = EvaTetherHand.Right;
                int changes = 0;
                for (float d = 2f; d < 6f; d += 0.004f)
                {
                    float rung = EvaTetherClimbMath.HoldingRung(d, sense, stroke, near);
                    float ahead = sense > 0 ? rung - d : d - rung;
                    Assert.That(ahead, Is.GreaterThan(near - 1e-4f).And.LessThanOrEqualTo(near + stroke + 1e-4f),
                        "the held rung is inside the reach band");
                    EvaTetherHand hand = EvaTetherClimbMath.HandForRung(rung, stroke);
                    if (!float.IsNaN(lastRung) && !Mathf.Approximately(rung, lastRung))
                    {
                        changes++;
                        Assert.That(Mathf.Abs(rung - lastRung), Is.EqualTo(stroke).Within(1e-4f), "one rung at a time");
                        Assert.That(hand, Is.Not.EqualTo(lastHand), "the other glove takes over");
                    }

                    lastRung = rung;
                    lastHand = hand;
                }

                Assert.That(changes, Is.InRange(7, 9), "about one change-over per half metre");
            }
        }

        [Test]
        public void PullSurge_AveragesTheRouteSpeedOverAWholePull()
        {
            const int steps = 20000;
            float time = 0f;
            for (int i = 0; i < steps; i++)
            {
                float phase = (i + 0.5f) / steps;
                time += (1f / steps) / RouteMath.PullSpeedFactor(phase, 0.3f);
            }

            Assert.That(1f / time, Is.EqualTo(1f).Within(1e-3f), "distance over time of one pull = route speed");
            Assert.That(RouteMath.PullSpeedFactor(0.5f, 0.3f), Is.GreaterThan(RouteMath.PullSpeedFactor(0f, 0.3f) * 1.8f));
        }

        [Test]
        public void Rope_FollowsTheDeliveredRoute_AtShoulderHeightOnTheHullSide_WithBoundedSag()
        {
            EvaTetherRope rope = Rope(Waypoints(Delivered, out _));
            Assert.That(rope.IsGeometryReady, Is.True);
            float length = RouteMath.Length(Delivered);
            Assert.That(rope.RouteLengthMetres, Is.EqualTo(length).Within(1e-4f));

            for (float d = 0f; d <= length; d += 0.05f)
            {
                Vector3 feet = rope.FeetPointAt(d);
                Vector3 offset = rope.PointAt(d) - feet;
                Assert.That(offset.y, Is.InRange(rope.HeightAboveFeetMetres - rope.MaxSagMetres - 0.002f,
                    rope.HeightAboveFeetMetres + 0.002f), "shoulder height minus at most the sag at d=" + d);
                Vector3 lateral = Vector3.ProjectOnPlane(offset, Vector3.up);
                Assert.That(lateral.magnitude, Is.InRange(0.3f, 2f * rope.LateralOffsetMetres + 0.06f), "beside the body, never through it, at d=" + d);
            }

            // The long stretch along the hull side (x = 4.35, heading -z): the rope runs between the body and the hull.
            Vector3 alongHull = rope.PointAt(10f);
            Assert.That(alongHull.x, Is.EqualTo(4.35f - rope.LateralOffsetMetres).Within(0.01f));

            // Anchors at both rope ends, at every corner and never further apart than the spacing.
            IReadOnlyList<float> anchors = rope.AnchorParams;
            float corner = 0f;
            for (int i = 1; i < Delivered.Length; i++)
            {
                float at = corner;
                Assert.That(anchors.Any(a => Mathf.Abs(a - at) < 0.06f), Is.True, "anchor at the corner " + (i - 1));
                corner += Vector3.Distance(Delivered[i - 1], Delivered[i]);
            }

            Assert.That(anchors.First(), Is.EqualTo(rope.MinParam).Within(1e-4f));
            Assert.That(anchors.Last(), Is.EqualTo(rope.MaxParam).Within(1e-4f));
            for (int i = 1; i < anchors.Count; i++)
            {
                Assert.That(anchors[i] - anchors[i - 1], Is.LessThanOrEqualTo(rope.MaxAnchorSpacingMetres + 1e-3f));
            }

            // Sag: zero at anchors, at most the maximum between them.
            foreach (float anchor in anchors)
            {
                Vector3 expected = rope.FeetPointAt(anchor);
                Assert.That(rope.PointAt(anchor).y - expected.y, Is.EqualTo(rope.HeightAboveFeetMetres).Within(0.06f));
            }
        }

        [Test]
        public void Rope_Build_MakesTheTexturedRopeClipsAndBrackets()
        {
            EvaTetherRope rope = Rope(Waypoints(Delivered, out _));
            Assert.That(rope.Build(), Is.True);
            Transform root = rope.GeneratedRoot;
            Assert.That(root, Is.Not.Null);
            created.Add(root.gameObject);
            string[] parts = { "Rope", "Clips", "Brackets" };
            foreach (string part in parts)
            {
                Transform child = root.Find(part);
                Assert.That(child, Is.Not.Null, part);
                Mesh mesh = child.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(100), part);
                Assert.That(mesh.triangles.Length % 3, Is.Zero);
                Assert.That(float.IsNaN(mesh.bounds.size.x), Is.False);
                Assert.That(child.GetComponent<Collider>(), Is.Null, "presentation only, no collider on " + part);
            }

            Mesh ropeMesh = root.Find("Rope").GetComponent<MeshFilter>().sharedMesh;
            Vector2[] uv = ropeMesh.uv;
            Assert.That(uv.Max(t => t.y), Is.GreaterThan(100f), "the braid repeats along the whole rope");
            Assert.That(rope.AnchorPoints.Count, Is.EqualTo(rope.BracketBases.Count), "one bracket per anchor");
        }

        [Test]
        public void ClosedGlove_HoldsTheRopeInItsFist_WithTheDeliveredForearm_AndANaturalWrist()
        {
            EvaTetherRope rope = Rope(Waypoints(Delivered, out _));
            float length = rope.RouteLengthMetres;
            int samples = 0;
            for (int sense = -1; sense <= 1; sense += 2)
            {
                for (float d = 1.6f; d < length - 0.8f; d += 0.11f)
                {
                    Vector3 eye = rope.FeetPointAt(d) + (Vector3.up * EvaTetherRope.EyeHeightMetres);
                    Vector3 forward = Vector3.ProjectOnPlane(rope.TangentAt(d) * sense, Vector3.up).normalized;
                    if (forward.sqrMagnitude < 0.5f)
                    {
                        continue;
                    }

                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    float side = Vector3.Dot(rope.PointAt(d) - eye, right) >= 0f ? 1f : -1f;
                    // Both ends of the reach band: a glove closing at full reach and a glove arriving by the chest.
                    float far = d + (sense * (rope.NearReachMetres + rope.HandStrokeMetres));
                    float close = d + (sense * (rope.NearReachMetres + 0.01f));
                    foreach (EvaTetherHand hand in new[] { EvaTetherHand.Right, EvaTetherHand.Left })
                    {
                        foreach (float station in new[] { far, close })
                        {
                            EvaTetherArmPose pose = AstronautHandsPresenter.SolveRopeGlove(rope, hand, station, 0f, sense, eye,
                                Vector3.up, forward, right, side);
                            Vector3 fist = pose.Wrist + (pose.Hand * AstronautHandsPresenter.TetherTubeCentreInHand);
                            Assert.That(Vector3.Distance(fist, rope.PointAt(station)), Is.LessThan(1e-3f), "rope inside the fist");
                            Assert.That(pose.Reachable, Is.True, hand + " reaches station " + station + " from d=" + d);
                            Assert.That(Vector3.Distance(pose.Elbow, pose.Wrist), Is.EqualTo(AstronautHandsPresenter.ToolForearmMetres).Within(0.003f));
                            Assert.That(pose.WristBendDegrees, Is.LessThanOrEqualTo(62f), hand + " wrist at d=" + d + " station " + station);
                            Assert.That(pose.RopeObliquityDegrees, Is.LessThanOrEqualTo(AstronautHandsPresenter.TetherMaxObliquityDegrees + 0.01f));
                            Vector3 ahead = pose.Wrist - eye;
                            Assert.That(Vector3.Dot(ahead, forward), Is.GreaterThan(0f), "the glove is in front of the body");
                            samples++;
                        }
                    }
                }
            }

            Assert.That(samples, Is.GreaterThan(200));
        }

        [Test]
        public void Installer_AddsOneRope_BindsTheHands_AndHidesTheOldGuide()
        {
            Transform[] points = Waypoints(Delivered, out GameObject routeObject);
            TetheredRoute route = routeObject.AddComponent<TetheredRoute>();
            route.Configure(points, 0.25f, 1f);
            LineRenderer guide = routeObject.AddComponent<LineRenderer>();
            guide.enabled = true;
            var handsObject = new GameObject("Tether test hands");
            created.Add(handsObject);
            AstronautHandsPresenter hands = handsObject.AddComponent<AstronautHandsPresenter>();

            EvaTetherRope rope = EvaTetherAuthoring.Install(route, hands);
            EvaTetherRope again = EvaTetherAuthoring.Install(route, hands);

            Assert.That(again, Is.SameAs(rope), "idempotent");
            Assert.That(routeObject.GetComponentsInChildren<EvaTetherRope>(true).Length, Is.EqualTo(1));
            Assert.That(rope.transform.parent, Is.SameAs(routeObject.transform));
            Assert.That(rope.Waypoints, Is.EqualTo(route.Waypoints), "the rope follows the gameplay waypoints themselves");
            Assert.That(rope.HandStrokeMetres, Is.EqualTo(route.HandStrokeMetres));
            Assert.That(rope.NearReachMetres, Is.EqualTo(route.NearReachMetres));
            Assert.That(hands.TetherRope, Is.SameAs(rope));
            Assert.That(guide.enabled, Is.False, "the amber guide line is replaced by the rope");
            EvaTetherArmSleeves[] sleeves = handsObject.GetComponentsInChildren<EvaTetherArmSleeves>(true);
            Assert.That(sleeves.Length, Is.EqualTo(1), "one set of arm sleeves, also after a second install");
            Assert.That(sleeves[0].Hands, Is.SameAs(hands));
        }
    }
}
