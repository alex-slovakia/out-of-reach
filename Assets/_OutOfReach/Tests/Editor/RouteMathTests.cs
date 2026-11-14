using NUnit.Framework;
using OutOfReach.Player;
using UnityEngine;

namespace OutOfReach.Tests.Editor
{
    /// <summary>The constrained EVA route: a corridor around a polyline, never past either end.</summary>
    public sealed class RouteMathTests
    {
        private static readonly Vector3[] Route =
        {
            new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, -4f),
        };

        [Test]
        public void Length_IsTheSumOfTheSegments()
        {
            Assert.That(RouteMath.Length(Route), Is.EqualTo(7f).Within(1e-5f));
        }

        [Test]
        public void ClosestPoint_ReportsTheDistanceAlongTheLine()
        {
            Vector3 onFirst = RouteMath.ClosestPoint(Route, new Vector3(1f, 0f, 0.4f), out float alongFirst);
            Vector3 onSecond = RouteMath.ClosestPoint(Route, new Vector3(3.2f, 0f, -2.5f), out float alongSecond);

            Assert.That(Vector3.Distance(onFirst, new Vector3(1f, 0f, 0f)), Is.LessThan(1e-5f));
            Assert.That(alongFirst, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Vector3.Distance(onSecond, new Vector3(3f, 0f, -2.5f)), Is.LessThan(1e-5f));
            Assert.That(alongSecond, Is.EqualTo(5.5f).Within(1e-5f));
        }

        [Test]
        public void Constrain_KeepsPositionsInsideTheCorridor_AndPullsOthersBack()
        {
            Vector3 inside = new Vector3(1f, 0.1f, 0.3f);
            Vector3 pulled = RouteMath.Constrain(Route, new Vector3(1f, 0f, 2f), 0.5f);

            Assert.That(RouteMath.Constrain(Route, inside, 0.5f), Is.EqualTo(inside), "free movement within the lateral freedom");
            Assert.That(Vector3.Distance(pulled, new Vector3(1f, 0f, 0.5f)), Is.LessThan(1e-5f), "clamped to 0.5 m from the line");
        }

        [Test]
        public void Constrain_NeverPassesEitherEndOfTheTether()
        {
            Vector3 beforeStart = RouteMath.Constrain(Route, new Vector3(-10f, 0f, 0f), 0.5f);
            Vector3 pastEnd = RouteMath.Constrain(Route, new Vector3(3f, 0f, -30f), 0.5f);

            Assert.That(Vector3.Distance(beforeStart, new Vector3(-0.5f, 0f, 0f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(pastEnd, new Vector3(3f, 0f, -4.5f)), Is.LessThan(1e-5f));
            RouteMath.ClosestPoint(Route, pastEnd, out float along);
            Assert.That(along, Is.EqualTo(7f).Within(1e-5f), "the tether length is the limit");
        }

        private static readonly Vector3[] Descent =
        {
            new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(2f, -3f, 0f), new Vector3(3f, -3f, 0f),
        };

        [Test]
        public void PointAtDistance_WalksTheLine_AndClampsToBothEnds()
        {
            Assert.That(Vector3.Distance(RouteMath.PointAtDistance(Route, 1.5f), new Vector3(1.5f, 0f, 0f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(RouteMath.PointAtDistance(Route, 3f), new Vector3(3f, 0f, 0f)), Is.LessThan(1e-5f), "the corner");
            Assert.That(Vector3.Distance(RouteMath.PointAtDistance(Route, 5f), new Vector3(3f, 0f, -2f)), Is.LessThan(1e-5f));
            Assert.That(RouteMath.PointAtDistance(Route, -4f), Is.EqualTo(Route[0]));
            Assert.That(RouteMath.PointAtDistance(Route, 40f), Is.EqualTo(Route[2]));
            Assert.That(RouteMath.PointAtDistance(Route, float.NaN), Is.EqualTo(Route[0]));
            Assert.That(Vector3.Distance(RouteMath.PointAtDistance(Descent, 3.5f), new Vector3(2f, -1.5f, 0f)), Is.LessThan(1e-5f), "vertical stretch");
        }

        [Test]
        public void PointAtDistance_IsContinuous_AndRoundTripsThroughClosestPoint()
        {
            Vector3 previous = RouteMath.PointAtDistance(Descent, 0f);
            for (float s = 0.01f; s <= 6f; s += 0.01f)
            {
                Vector3 point = RouteMath.PointAtDistance(Descent, s);
                Assert.That(Vector3.Distance(point, previous), Is.LessThanOrEqualTo(0.0101f), "no jump at " + s);
                RouteMath.ClosestPoint(Descent, point, out float along);
                Assert.That(along, Is.EqualTo(s).Within(1e-4f));
                previous = point;
            }
        }

        [Test]
        public void HorizontalHeading_FollowsTheLine_AndFallsBackToTheOverallHeadingOnVerticalStretches()
        {
            Vector3 east = new Vector3(1f, 0f, 0f);
            Assert.That(Vector3.Distance(RouteMath.HorizontalHeading(Route, 1f, 0.5f), east), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(RouteMath.HorizontalHeading(Route, 5.5f, 0.5f), new Vector3(0f, 0f, -1f)), Is.LessThan(1e-5f));
            Vector3 corner = RouteMath.HorizontalHeading(Route, 3f, 1.5f);
            Assert.That(corner.x, Is.EqualTo(0.7071f).Within(1e-3f), "diagonal across the corner");
            Assert.That(corner.z, Is.EqualTo(-0.7071f).Within(1e-3f));

            Assert.That(Vector3.Distance(RouteMath.HorizontalHeading(Descent, 3.5f, 1f), east), Is.LessThan(1e-5f), "vertical stretch: overall heading");
            Assert.That(RouteMath.HorizontalHeading(Descent, 3.5f, 1f).y, Is.EqualTo(0f));
            Assert.That(RouteMath.HorizontalHeading(new[] { Vector3.zero, new Vector3(0f, -5f, 0f) }, 2f, 1f), Is.EqualTo(Vector3.zero), "a vertical route has no heading");
        }

        [Test]
        public void DegenerateRoutes_DoNotThrow()
        {
            var single = new[] { new Vector3(1f, 2f, 3f) };

            Assert.That(RouteMath.ClosestPoint(single, Vector3.zero, out float along), Is.EqualTo(single[0]));
            Assert.That(along, Is.EqualTo(0f));
            Assert.That(RouteMath.Constrain(new Vector3[0], new Vector3(5f, 5f, 5f), 0.5f), Is.EqualTo(new Vector3(5f, 5f, 5f)));
            Assert.That(RouteMath.Length(single), Is.EqualTo(0f));
        }
    }
}
