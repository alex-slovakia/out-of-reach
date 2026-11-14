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
