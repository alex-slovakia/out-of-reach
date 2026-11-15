using System;
using UnityEngine;

namespace OutOfReach.Presentation.CelestialSky
{
    /// <summary>
    /// Pure orientation maths for the NASA SVS celestial sky. The sky frame is the one the panoramic
    /// lookup uses: +X = RA 0h, +Y = celestial north, +Z = RA 6h (ICRF/J2000 axes, swapped into Unity's
    /// Y-up layout, which is a proper non-mirroring change of handedness).
    /// AURORA-7's attitude is fictional; this chooses the attitude that puts the real Sun (Horizons) where
    /// the scene's authored light comes from and keeps ecliptic north as close to world up as possible.
    /// </summary>
    public static class CelestialSkyFrame
    {
        public const int SampleCount = 32;
        public const double SecondsPerDay = 86400d;
        /// <summary>Bootstrap "Sunlight - authored direction" default, Euler(34, -55, 0).</summary>
        public static readonly Vector3 DefaultLightEuler = new Vector3(34f, -55f, 0f);

        /// <summary>Last daily sample at or before the elapsed time (index 0 = 2026-11-14 08:00 UTC), clamped.</summary>
        public static int DayIndex(double elapsedSimulationSeconds)
        {
            if (double.IsNaN(elapsedSimulationSeconds) || elapsedSimulationSeconds <= 0d) return 0;
            if (double.IsInfinity(elapsedSimulationSeconds)) return SampleCount - 1;
            double day = Math.Floor(elapsedSimulationSeconds / SecondsPerDay);
            return day >= SampleCount - 1 ? SampleCount - 1 : (int)day;
        }

        /// <summary>
        /// TRIAD attitude. Returns q with q * sunWorld == sunSky exactly, and q * upWorld in the
        /// half-plane spanned by sunSky and upSky (angles are preserved, so it is as close as possible).
        /// </summary>
        public static Quaternion SkyFromWorld(Vector3 sunWorld, Vector3 sunSky, Vector3 upWorld, Vector3 upSky)
        {
            if (sunWorld.sqrMagnitude < 1e-12f || sunSky.sqrMagnitude < 1e-12f) return Quaternion.identity;
            sunWorld.Normalize();
            sunSky.Normalize();
            if (Vector3.Cross(sunWorld, upWorld).sqrMagnitude < 1e-8f) upWorld = Vector3.forward;
            if (Vector3.Cross(sunSky, upSky).sqrMagnitude < 1e-8f) upSky = Vector3.right;
            Quaternion sky = Quaternion.LookRotation(sunSky, upSky);
            Quaternion world = Quaternion.LookRotation(sunWorld, upWorld);
            return Normalised(sky * Quaternion.Inverse(world));
        }

        /// <summary>Sky-from-world rotation for a daily sample and a directional light's forward vector.</summary>
        public static Quaternion ForDay(int day, Vector3 lightForward)
        {
            day = Mathf.Clamp(day, 0, SampleCount - 1);
            return SkyFromWorld(-lightForward, CelestialEphemeris2026.SunFromObserver[day],
                Vector3.up, CelestialEphemeris2026.EclipticNorth);
        }

        /// <summary>World-space unit direction of a sky-frame direction.</summary>
        public static Vector3 ToWorld(Quaternion skyFromWorld, Vector3 skyDirection)
        {
            return (Quaternion.Inverse(skyFromWorld) * skyDirection).normalized;
        }

        /// <summary>ICRF/J2000 right ascension and declination in degrees to the sky frame.</summary>
        public static Vector3 FromRaDec(double rightAscensionDeg, double declinationDeg)
        {
            double ra = rightAscensionDeg * Math.PI / 180d, dec = declinationDeg * Math.PI / 180d;
            double c = Math.Cos(dec);
            return new Vector3((float)(c * Math.Cos(ra)), (float)Math.Sin(dec), (float)(c * Math.Sin(ra)));
        }

        public static Vector4 ToShaderVector(Quaternion q)
        {
            return new Vector4(q.x, q.y, q.z, q.w);
        }

        /// <summary>Sun for the shader: sky-frame direction (xyz) and true angular radius in radians (w).</summary>
        public static Vector4 SunShaderVector(int day)
        {
            day = Mathf.Clamp(day, 0, SampleCount - 1);
            Vector3 s = CelestialEphemeris2026.SunFromObserver[day];
            return new Vector4(s.x, s.y, s.z, CelestialEphemeris2026.SunAngularRadius[day]);
        }

        /// <summary>Earth for the shader: sky-frame direction (xyz) and flux in 8K-map units x steradian (w).</summary>
        public static Vector4 EarthShaderVector(int day)
        {
            day = Mathf.Clamp(day, 0, SampleCount - 1);
            Vector3 e = CelestialEphemeris2026.EarthFromObserver[day];
            return new Vector4(e.x, e.y, e.z, CelestialEphemeris2026.EarthMapFlux[day]);
        }

        private static Quaternion Normalised(Quaternion q)
        {
            float n = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (n < 1e-6f) return Quaternion.identity;
            // Canonical hemisphere (w >= 0) keeps serialized values stable; q and -q are the same rotation.
            float s = q.w < 0f ? -1f / n : 1f / n;
            return new Quaternion(q.x * s, q.y * s, q.z * s, q.w * s);
        }
    }
}
