using NUnit.Framework;
using OutOfReach.Presentation.CelestialSky;
using UnityEngine;

namespace OutOfReach.Tests.CelestialSky
{
    public sealed class CelestialSkyFrameTests
    {
        private const float Tolerance = 2e-4f;
        private static Vector3 DefaultLightForward => Quaternion.Euler(CelestialSkyFrame.DefaultLightEuler) * Vector3.forward;

        private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance = Tolerance)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(tolerance), $"expected {expected:F5}, got {actual:F5}");
        }

        [Test]
        public void TableHasOneUnitVectorPerDailySample()
        {
            Assert.That(CelestialEphemeris2026.SunFromObserver.Length, Is.EqualTo(CelestialSkyFrame.SampleCount));
            Assert.That(CelestialEphemeris2026.EarthFromObserver.Length, Is.EqualTo(CelestialSkyFrame.SampleCount));
            foreach (Vector3 v in CelestialEphemeris2026.SunFromObserver) Assert.That(v.magnitude, Is.EqualTo(1f).Within(1e-5f));
            foreach (Vector3 v in CelestialEphemeris2026.EarthFromObserver) Assert.That(v.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void SunFromMarsLiesOnTheEclipticWithinMarsInclination()
        {
            foreach (Vector3 sun in CelestialEphemeris2026.SunFromObserver)
                Assert.That(Mathf.Abs(Vector3.Dot(sun, CelestialEphemeris2026.EclipticNorth)), Is.LessThan(Mathf.Sin(1.86f * Mathf.Deg2Rad)));
        }

        [Test]
        public void RuntimeTriadMatchesGeneratedDefaultRotation()
        {
            Quaternion q = CelestialSkyFrame.ForDay(0, DefaultLightForward);
            Assert.That(Mathf.Abs(Quaternion.Dot(q, CelestialEphemeris2026.DefaultSkyFromWorld)), Is.GreaterThan(1f - 1e-6f));
        }

        [Test]
        public void SunlightComesFromTheEphemerisSunEveryDay()
        {
            Vector3 forward = Quaternion.Euler(26f, 220f, 0f) * Vector3.forward; // any authored light
            for (int day = 0; day < CelestialSkyFrame.SampleCount; day++)
            {
                Quaternion q = CelestialSkyFrame.ForDay(day, forward);
                AssertClose(-forward, CelestialSkyFrame.ToWorld(q, CelestialEphemeris2026.SunFromObserver[day]));
            }
        }

        [Test]
        public void EclipticNorthIsAsCloseToWorldUpAsTheLightAllows()
        {
            Quaternion q = CelestialSkyFrame.ForDay(0, DefaultLightForward);
            Vector3 eclipticNorthWorld = CelestialSkyFrame.ToWorld(q, CelestialEphemeris2026.EclipticNorth);
            Vector3 sunWorld = -DefaultLightForward;
            // Coplanar with Sun and world up, on the up side: the TRIAD optimum.
            Assert.That(Vector3.Dot(Vector3.Cross(sunWorld, Vector3.up).normalized, eclipticNorthWorld), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Vector3.Angle(Vector3.up, eclipticNorthWorld), Is.EqualTo(35.554f).Within(0.01f));
        }

        [Test]
        public void ReferenceStarsLandWhereTheOfflineDerivationPutsThem()
        {
            Quaternion q = CelestialEphemeris2026.DefaultSkyFromWorld;
            AssertClose(new Vector3(-0.1889f, -0.9662f, 0.1756f), CelestialSkyFrame.ToWorld(q, CelestialSkyFrame.FromRaDec(101.2872, -16.7161)), 2e-3f); // Sirius
            AssertClose(new Vector3(-0.6196f, 0.5200f, 0.5880f), CelestialSkyFrame.ToWorld(q, CelestialSkyFrame.FromRaDec(37.9529, 89.2641)), 2e-3f);   // Polaris
            AssertClose(new Vector3(0.4756f, 0.4658f, -0.7462f), CelestialSkyFrame.ToWorld(q, CelestialEphemeris2026.GalacticCentre), 2e-3f);
            AssertClose(new Vector3(0.8935f, 0.4315f, 0.1243f), CelestialSkyFrame.ToWorld(q, CelestialEphemeris2026.EarthFromObserver[0]), 2e-3f);
        }

        [Test]
        public void PanoramicFrameMatchesSvsPixelLayout()
        {
            // Unity Skybox/Panoramic: u = 0.5 - atan2(z, x) / 2pi. SVS starmap: u = 0.5 - RA / 360.
            Vector3 sirius = CelestialSkyFrame.FromRaDec(101.2872, -16.7161);
            float u = 0.5f - Mathf.Atan2(sirius.z, sirius.x) / (2f * Mathf.PI);
            Assert.That(u * 4096f, Is.EqualTo(895.6f).Within(0.5f)); // peak found at x 894-895 in the source EXR
        }

        [Test]
        public void DayIndexUsesLastSampleAtOrBeforeAndClamps()
        {
            Assert.That(CelestialSkyFrame.DayIndex(double.NaN), Is.EqualTo(0));
            Assert.That(CelestialSkyFrame.DayIndex(-5), Is.EqualTo(0));
            Assert.That(CelestialSkyFrame.DayIndex(86399.9), Is.EqualTo(0));
            Assert.That(CelestialSkyFrame.DayIndex(86400), Is.EqualTo(1));
            Assert.That(CelestialSkyFrame.DayIndex(40 * 86400.0), Is.EqualTo(31));
            Assert.That(CelestialSkyFrame.DayIndex(double.PositiveInfinity), Is.EqualTo(31));
        }
    }
}
